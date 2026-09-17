using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// The catalog models the actual domain: body types, equipment slots, multi-part
// items, texture variants, and separately supplied animation clips.
sealed class CharacterCatalog
{
    public string name { get; set; } = "";
    public string defaultBody { get; set; } = "";
    public List<CharacterBody> bodies { get; set; } = [];
    public string defaultPose { get; set; } = "rest";
    [JsonIgnore] public List<(string Path, string Role)> Scenes { get; } = [];
    [JsonIgnore] public Dictionary<string, string[]> SceneTextures { get; } = [];

    public static CharacterCatalog Load(string recipe, Dictionary<string, ZipArchiveEntry> entries)
    {
        var catalog = JsonSerializer.Deserialize<CharacterCatalog>(File.ReadAllText(recipe))
            ?? throw new InvalidDataException("Invalid character recipe");
        using var itemStream = entries["xml/item.x7"].Open();
        var itemXml = XDocument.Load(itemStream).Root!.Elements("item").ToLookup(e => (string)e.Attribute("item_key")!);
        using var labelStream = entries["language/xml/iteminfo_string_table.x7"].Open();
        var labels = XDocument.Load(labelStream).Root!.Elements("string").GroupBy(e => (string?)e.Attribute("key") ?? "")
            .ToDictionary(g => g.Key, g => (string?)g.First().Attribute("eng"));
        using var recipeDoc = JsonDocument.Parse(File.ReadAllText(recipe));
        var recipes = recipeDoc.RootElement.GetProperty("bodies").EnumerateArray().ToArray();
        // The recipe's item list contains identifiers; the output contains resolved items.
        for (var b = 0; b < catalog.bodies.Count; b++)
        {
            var body = catalog.bodies[b];
            catalog.Scenes.Add((body.skeleton, "skeleton"));
            foreach (var id in recipes[b].GetProperty("importItems").EnumerateArray().Select(v => v.GetString()!))
            {
                var matches = itemXml[id].ToArray();
                if (matches.Length != 1) throw new InvalidDataException($"Expected one definition for item {id}, found {matches.Length}");
                var xml = matches[0];
                var definition = xml.Element("base")!;
                var sex = (string?)definition.Attribute("sex");
                if (sex != body.sourceSex && sex is not ("unisex" or "all"))
                    throw new InvalidDataException($"Item {id} ({sex}) does not fit {body.id}");
                var item = ResolveItem(xml, labels, (reference, context) =>
                {
                    var scene = context + reference.ToLowerInvariant();
                    if (!entries.ContainsKey(scene)) throw new FileNotFoundException(scene);
                    return scene;
                });
                catalog.Scenes.AddRange(item.parts.Select(p => (p.scene, "equipment")));
                body.items.Add(item);
            }
            foreach (var (slot, id) in body.defaults)
                if (!body.items.Any(i => i.id == id && i.slot == slot)) throw new InvalidDataException("Invalid default equipment: " + id);
        }
        if (!catalog.bodies.Any(b => b.id == catalog.defaultBody)) throw new InvalidDataException("Unknown default body");
        return catalog;
    }

    public static CharacterItem ResolveItem(XElement xml, Dictionary<string, string?> labels, Func<string, string, string> resolve)
    {
        var id = (string?)xml.Attribute("item_key") ?? "";
        var definition = xml.Element("base") ?? throw new InvalidDataException("No base for " + id);
        if (!Regex.IsMatch(id, @"^10[0-6][0-9]{4}$")) throw new InvalidDataException("Malformed costume identifier: " + id);
        var category = (int.Parse(id) / 10000) % 100;
        var (slot, folder) = category switch
        {
            0 => ("hair", "hair"), 1 => ("face", "face"), 2 => ("shirt", "body"),
            3 => ("pants", "leg"), 4 => ("gloves", "hand"), 5 => ("shoes", "foot"),
            6 => ("accessory", "acc"), _ => throw new InvalidDataException("Not a costume item: " + id)
        };
        var graphic = xml.Element("graphic") ?? throw new InvalidDataException("No graphic for " + id);
        var item = new CharacterItem
        {
            id = id, slot = slot, label = labels.GetValueOrDefault((string?)definition.Attribute("name_key") ?? "") ?? id,
            hides = ((string?)graphic.Attribute("hiding_option") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        };
        foreach (var attribute in graphic.Attributes().Where(a => a.Name.LocalName == "to_part_scene_file" || Regex.IsMatch(a.Name.LocalName, @"^to_node_scene_file\d+$")))
        {
            if (string.IsNullOrWhiteSpace(attribute.Value)) continue;
            var suffix = attribute.Name.LocalName.Replace("to_node_scene_file", "");
            var attachment = attribute.Name.LocalName == "to_part_scene_file" ? null : (string?)graphic.Attribute("to_node_parent_node" + suffix);
            var scene = resolve(attribute.Value, "resources/model/character/" + folder + "/");
            item.parts.Add(new(scene, attachment));
        }
        if (item.parts.Count == 0) throw new InvalidDataException("Item has no resolved parts: " + id);
        return item;
    }

    public void ResolveVariants(Dictionary<string, ZipArchiveEntry> entries, Action<string> preserve)
    {
        foreach (var item in bodies.SelectMany(b => b.items))
        {
            var originals = item.parts.SelectMany(p => SceneTextures.GetValueOrDefault(p.scene, [])).Distinct().ToArray();
            item.variants.Add(new("default", "Original", []));
            var choices = new SortedDictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (var original in originals)
            {
                var stem = Path.ChangeExtension(original, null);
                var pattern = "^" + Regex.Escape(stem) + @"_(\d+)\.dds$";
                foreach (var candidate in entries.Keys)
                {
                    var match = Regex.Match(candidate, pattern);
                    if (!match.Success) continue;
                    var id = match.Groups[1].Value;
                    if (!choices.TryGetValue(id, out var maps)) choices[id] = maps = [];
                    maps[original] = candidate;
                }
                if (original.Contains("_atex"))
                {
                    var enemy = original.Replace("_atex", "_etex");
                    if (entries.ContainsKey(enemy))
                    {
                        if (!choices.TryGetValue("enemy", out var maps)) choices["enemy"] = maps = [];
                        maps[original] = enemy;
                    }
                }
            }
            foreach (var (id, maps) in choices)
            {
                foreach (var path in maps.Values) preserve(path);
                item.variants.Add(new(id, id == "enemy" ? "Enemy team" : "Variant " + id, maps));
            }
        }
    }
}
sealed class CharacterBody
{
    public string id { get; set; } = "";
    public string label { get; set; } = "";
    public string sourceSex { get; set; } = "";
    public string skeleton { get; set; } = "";
    public Dictionary<string, string> defaults { get; set; } = [];
    public List<CharacterItem> items { get; set; } = [];
    // Future entries point to separately converted clips, not hard-coded UI options.
    public List<CharacterAnimation> animations { get; set; } = [];
}
sealed class CharacterItem
{
    public string id { get; set; } = "";
    public string slot { get; set; } = "";
    public string label { get; set; } = "";
    public string[] hides { get; set; } = [];
    public List<CharacterPart> parts { get; set; } = [];
    public List<CharacterVariant> variants { get; set; } = [];
}
record CharacterPart(string scene, string? attachmentBone);
record CharacterVariant(string id, string label, Dictionary<string, string> maps);
record CharacterAnimation(string id, string label, string url);
