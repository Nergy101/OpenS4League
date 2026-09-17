using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

record WardrobeInventory(string id, string label, string slot, string sex, string status, string? reason);
record WardrobeScene(string json, string bin);
record TextureVariant(string file, int width, int height, string algorithm, int sourceWidth, int sourceHeight, string sourceSha256, string generatedSha256);
record WardrobeTexture(string kind, Dictionary<string, TextureVariant> variants);
record SceneResolution(string requestedReference, string requestedPath, string resolvedPath, string method, string reason);

// Bulk orchestration only: SCN parsing/serialization and item mapping are shared.
static class WardrobeConverter
{
    static readonly JsonSerializerOptions Options = new() { IncludeFields = true, WriteIndented = true };
    static readonly string[] Slots = ["hair", "face", "shirt", "pants", "gloves", "shoes", "accessory"];
    static readonly string[] Folders = ["hair", "face", "body", "leg", "hand", "foot", "acc"];

    public static void Run(Dictionary<string, ZipArchiveEntry> entries, string output, string archiveName, string[]? requestedQualities = null, string algorithm = "deterministic-bilinear")
    {
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(Path.Combine(output, "scenes"));
        var qualities = (requestedQualities is null || requestedQualities.Length == 0 ? ["1x"] : requestedQualities)
            .Distinct(StringComparer.Ordinal).OrderBy(q => q switch { "1x" => 1, "2x" => 2, "4x" => 4, _ => 0 }).ToArray();
        if (qualities.Any(q => q is not ("1x" or "2x" or "4x")) || !qualities.Contains("1x"))
            throw new ArgumentException("--texture-quality must contain 1x and only use 1x,2x,4x");
        var assets = new ConversionAssets(entries, output);
        var scenes = new SortedDictionary<string, WardrobeScene>(StringComparer.Ordinal);
        var sceneTextures = new Dictionary<string, string[]>();
        var sceneErrors = new SortedDictionary<string, string>();
        var missingSceneReferences = new SortedDictionary<string, string>();
        var missingTextureReferences = new SortedDictionary<string, string>();
        var sceneResolutions = new Dictionary<string, SceneResolution>();
        var textures = new SortedDictionary<string, WardrobeTexture>(StringComparer.Ordinal);
        var textureErrors = new SortedDictionary<string, string>();
        var variantErrors = new List<object>();
        var skinWarnings = new List<object>();
        var models = 0; var vertices = 0; var triangles = 0; var maxInfluences = 0;
        var parsedScenes = 0;
        var catalog = new CharacterCatalog { name = "Female wardrobe", defaultBody = "female" };
        var body = new CharacterBody { id = "female", label = "Female", sourceSex = "woman", skeleton = "resources/model/character/female_bip.scn" };
        catalog.bodies.Add(body);
        foreach (var key in new[] { "xml/item.x7", "xml/default_item.x7", "language/xml/iteminfo_string_table.x7" }) assets.Copy(entries[key]);
        using var itemStream = entries["xml/item.x7"].Open();
        var allItems = XDocument.Load(itemStream).Root!.Elements("item").ToArray();
        using var labelsStream = entries["language/xml/iteminfo_string_table.x7"].Open();
        var labels = XDocument.Load(labelsStream).Root!.Elements("string").GroupBy(e => (string?)e.Attribute("key") ?? "")
            .ToDictionary(g => g.Key, g => (string?)g.First().Attribute("eng"));
        using var defaultsStream = entries["xml/default_item.x7"].Open();
        foreach (var group in XDocument.Load(defaultsStream).Root!.Element("female")!.Elements("item").GroupBy(e => (int)e.Attribute("sub_category")!))
        {
            var item = group.First();
            if (group.Key is < 0 or > 5) continue;
            body.defaults[Slots[group.Key]] = ((int)item.Attribute("category")! * 1000000 + group.Key * 10000 + (int)item.Attribute("number")!).ToString();
        }
        // Prefix filtering retains malformed costume identifiers for explicit diagnostics;
        // exact format validation happens before any item/scene lookup.
        var requested = allItems.Where(e => Regex.IsMatch((string?)e.Attribute("item_key") ?? "", @"^10[0-6]")
            && (string?)e.Element("base")?.Attribute("sex") is "woman" or "unisex").OrderBy(e => (string?)e.Attribute("item_key"), StringComparer.Ordinal).ToArray();
        var duplicates = requested.GroupBy(e => (string?)e.Attribute("item_key") ?? "").Where(g => g.Count() != 1).Select(g => g.Key).ToHashSet();
        EnsureScene(body.skeleton, "skeleton");
        if (sceneErrors.TryGetValue(body.skeleton, out var rigError)) throw new InvalidDataException("Default rig unavailable: " + rigError);
        var checkpoint = Path.Combine(output, "inventory.jsonl");
        using (var journal = new StreamWriter(checkpoint, false, new UTF8Encoding(false)))
        {
            var completed = 0;
            foreach (var xml in requested)
            {
                var id = (string?)xml.Attribute("item_key") ?? "";
                var slot = Slots[id[2] - '0'];
                var sex = (string?)xml.Element("base")?.Attribute("sex") ?? "";
                var label = labels.GetValueOrDefault((string?)xml.Element("base")?.Attribute("name_key") ?? "") ?? id;
                string? reason = null;
                try
                {
                    if (!Regex.IsMatch(id, @"^10[0-6][0-9]{4}$")) throw new InvalidDataException("Malformed costume identifier: " + id);
                    if (duplicates.Contains(id)) throw new InvalidDataException("Duplicate costume identifier: " + id);
                    // Attempt every part even if a sibling is missing. No guessed fallback assets.
                    var errors = new List<string>();
                    var context = "resources/model/character/" + Folders[id[2] - '0'] + "/";
                    foreach (var reference in xml.Element("graphic")?.Attributes().Where(a => a.Name.LocalName == "to_part_scene_file" || Regex.IsMatch(a.Name.LocalName, @"^to_node_scene_file\d+$")).Select(a => a.Value).Where(v => !string.IsNullOrWhiteSpace(v)) ?? [])
                    {
                        try { var path = ResolveScene(reference, context); EnsureScene(path, "equipment"); if (sceneErrors.TryGetValue(path, out var error)) errors.Add(error); }
                        catch (Exception ex) when (Recoverable(ex)) { errors.Add(ex.Message); }
                    }
                    if (errors.Count != 0) throw new InvalidDataException(string.Join("; ", errors.Distinct()));
                    var item = CharacterCatalog.ResolveItem(xml, labels, ResolveScene);
                    AddVariants(item);
                    body.items.Add(item);
                }
                catch (Exception ex) when (Recoverable(ex)) { reason = ex.Message; }
                journal.WriteLine(JsonSerializer.Serialize(new WardrobeInventory(id, label, slot, sex, reason is null ? "converted" : "unavailable", reason)));
                // Durable medium batches; final totals are computed from the saved journal.
                if (++completed % 25 == 0) { journal.Flush(); Console.WriteLine($"Wardrobe checkpoint: {completed}/{requested.Length} records, {scenes.Count} scenes, {textures.Count} textures"); }
            }
        }
        var inventory = File.ReadLines(checkpoint).Select(line => JsonSerializer.Deserialize<WardrobeInventory>(line)!).ToArray();
        if (inventory.Length != requested.Length) throw new InvalidDataException("Incomplete inventory checkpoint");
        foreach (var (slot, id) in body.defaults)
            if (!body.items.Any(i => i.id == id && i.slot == slot)) throw new InvalidDataException("Default equipment unavailable: " + id);
        var coverage = new
        {
            requestedItems = inventory.Length, convertedItems = inventory.Count(i => i.status == "converted"), unavailableItems = inventory.Count(i => i.status == "unavailable"),
            bySex = inventory.GroupBy(i => i.sex).ToDictionary(g => g.Key, g => g.Count()),
            bySlot = inventory.GroupBy(i => i.slot).ToDictionary(g => g.Key, g => new { requested = g.Count(), converted = g.Count(i => i.status == "converted"), unavailable = g.Count(i => i.status == "unavailable") }),
            scenes = scenes.Count, attemptedScenes = scenes.Count + sceneErrors.Count, parsedScenes, failedScenes = sceneErrors,
            missingSceneReferences, missingSceneReferenceCount = missingSceneReferences.Count,
            missingTextureReferences, missingTextureReferenceCount = missingTextureReferences.Count,
            relocatedScenes = sceneResolutions.Values.Count(r => r.method == "unique-basename"),
            textures = textures.Count, failedTextures = textureErrors, failedTextureCount = textureErrors.Count,
            variants = body.items.Sum(i => i.variants.Count), variantIssues = variantErrors, variantIssueCount = variantErrors.Count,
            models, vertices, triangles, maxInfluences, skinWarnings, skinWarningCount = skinWarnings.Count,
            dependencies = assets.dependencies.Count,
            textureQuality = new { requested = qualities, algorithm, generatedVariants = textures.Values.Sum(t => t.variants.Count) }
        };
        var manifest = new { format = "s4-wardrobe", version = 1, catalog, scenes, textures, inventory, coverage,
            provenance = new { sceneResolutions = sceneResolutions.Values.OrderBy(r => r.requestedPath, StringComparer.Ordinal).ToArray() },
            sourceArchive = archiveName, sourceConfig = "xml/item.x7", dependencies = assets.dependencies, aliases = assets.aliases,
            coordinates = "Original S4 units, Y up; row-vector matrices serialized for Three.js column-major arrays. Loader reflects Z once for left-to-right handedness." };
        WriteJson("coverage.json", coverage);
        WriteJson("index.json", manifest);
        Console.WriteLine(JsonSerializer.Serialize(coverage, Options));

        string ResolveScene(string reference, string context)
        {
            var entry = assets.Resolve(reference, context);
            if (entry is null)
            {
                missingSceneReferences[reference] = $"Missing scene reference '{reference}' in '{context}'";
                throw new FileNotFoundException(missingSceneReferences[reference]);
            }
            var resolved = entry.FullName[5..].ToLowerInvariant();
            var key = reference.Replace('\\', '/').ToLowerInvariant();
            var method = resolved == key ? "exact-path" : resolved == context + key || resolved == context + Path.GetFileName(key) ? "context-path" : "unique-basename";
            var requestedPath = context + reference;
            sceneResolutions[requestedPath] = new(reference, requestedPath, resolved, method,
                method == "unique-basename" ? "Exact/context path absent; exactly one archive entry has this basename. No filename was changed or guessed." : "Source reference resolved directly without filename substitution.");
            return resolved;
        }
        string PreserveTexture(string reference, string context)
        {
            var path = assets.Preserve(reference, context);
            if (path is null)
            {
                missingTextureReferences[reference] = $"Missing texture reference '{reference}' in '{context}'";
                throw new FileNotFoundException(missingTextureReferences[reference]);
            }
            EnsureTexture(path);
            return path;
        }
        void EnsureTexture(string path)
        {
            if (textureErrors.TryGetValue(path, out var failure)) throw new InvalidDataException(failure);
            if (textures.ContainsKey(path)) return;
            try
            {
                assets.Copy(entries[path]);
                var sourceBytes = assets.Read(entries[path]);
                var source = PngTexture.Decode(sourceBytes);
                var sourceSha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(sourceBytes));
                var variants = new Dictionary<string, TextureVariant>(StringComparer.Ordinal);
                var kind = Regex.IsMatch(path, @"(?:_n|normal)\.(dds|tga|bmp|png)$", RegexOptions.IgnoreCase) ? "normal" : "color";
                foreach (var quality in qualities)
                {
                    var scale = quality switch { "1x" => 1, "2x" => 2, "4x" => 4, _ => throw new ArgumentException(quality) };
                    var file = "textures/" + StableId(path) + "." + quality + ".png";
                    var destination = Path.Combine(output, file);
                    PngTexture.Write(PngTexture.Resize(source, scale), destination);
                    variants[quality] = new(file, source.Width * scale, source.Height * scale, algorithm, source.Width, source.Height, sourceSha, PngTexture.Sha256(destination));
                }
                textures[path] = new(kind, variants);
            }
            catch (Exception ex) when (Recoverable(ex))
            {
                textureErrors[path] = $"Texture decode failed '{path}': {ex.Message}";
                throw new InvalidDataException(textureErrors[path], ex);
            }
        }
        void EnsureScene(string path, string role)
        {
            if (scenes.ContainsKey(path) || sceneErrors.ContainsKey(path)) return;
            var prefix = "scenes/" + StableId(path);
            try
            {
                assets.Copy(entries[path]);
                using var binary = new BinaryWriter(File.Create(Path.Combine(output, prefix + ".bin")));
                var result = SceneExporter.Export(entries[path], role, binary, PreserveTexture);
                parsedScenes++;
                var scene = JsonSerializer.SerializeToElement(result.Scene, Options);
                foreach (var node in scene.GetProperty("nodes").EnumerateArray().Where(n => n.GetProperty("geometry").ValueKind != JsonValueKind.Null))
                {
                    var counts = new Dictionary<uint, int>();
                    foreach (var bone in node.GetProperty("details").GetProperty("bones").EnumerateArray())
                        foreach (var weight in bone.GetProperty("Weight").EnumerateArray())
                        {
                            var vertex = weight.GetProperty("Vertex").GetUInt32();
                            if (vertex >= node.GetProperty("geometry").GetProperty("positions").GetProperty("count").GetInt32()) throw new InvalidDataException("Invalid skin vertex in " + path);
                            if (weight.GetProperty("Weight").GetSingle() != 0) counts[vertex] = counts.GetValueOrDefault(vertex) + 1;
                        }
                    var max = counts.Values.DefaultIfEmpty(0).Max();
                    maxInfluences = Math.Max(maxInfluences, max);
                    if (max > 4) skinWarnings.Add(new { scene = path, node = node.GetProperty("name").GetString(), maxInfluences = max, verticesOverFour = counts.Count(p => p.Value > 4), reason = "All source weights preserved; runtime must support more than four influences." });
                }
                binary.Flush();
                WriteJson(prefix + ".json", result.Scene);
                scenes[path] = new(prefix + ".json", prefix + ".bin");
                sceneTextures[path] = result.DiffuseTextures;
                models += result.Models; vertices += result.Vertices; triangles += result.Triangles;
            }
            catch (Exception ex) when (Recoverable(ex))
            {
                sceneErrors[path] = $"Scene '{path}': {ex.Message}";
                // A failed scene never leaves a plausible, partially written asset behind.
                foreach (var suffix in new[] { ".bin", ".json", ".json.tmp" }) File.Delete(Path.Combine(output, prefix + suffix));
            }
        }
        void AddVariants(CharacterItem item)
        {
            item.variants.Add(new("default", "Original", []));
            var choices = new SortedDictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (var original in item.parts.SelectMany(p => sceneTextures.GetValueOrDefault(p.scene, [])).Distinct())
            {
                // A missing conventional team counterpart is an explicit optional
                // variant issue, not an invented texture or an unavailable base item.
                if (Regex.IsMatch(original, @"_[ae]tex(?:_\d+)?\.(dds|tga|bmp|png)$"))
                {
                    var counterpart = original.Contains("_atex") ? original.Replace("_atex", "_etex") : original.Replace("_etex", "_atex");
                    if (!entries.ContainsKey(counterpart)) variantErrors.Add(new { item = item.id, source = original, texture = counterpart, kind = "missing-team-texture", reason = "Optional team counterpart is absent from the source archive; no replacement was synthesized." });
                }
                // Only color-family numeric/team suffixes; _n normal maps are not skins.
                var stem = Regex.Replace(Path.ChangeExtension(original, null), @"_\d+$", "");
                var family = Regex.Replace(stem, @"_[ae]tex$", "");
                var pattern = "^" + Regex.Escape(family) + @"(?<team>_[ae]tex)?(?:_(?<number>\d+))?\.(dds|tga|bmp|png)$";
                foreach (var candidate in entries.Keys.Order(StringComparer.Ordinal))
                {
                    var match = Regex.Match(candidate, pattern);
                    if (!match.Success || candidate == original) continue;
                    var team = match.Groups["team"].Value;
                    var id = (team.Length == 0 ? "" : team == "_atex" ? "ally-" : "enemy-") + match.Groups["number"].Value;
                    if (id == "") id = "base";
                    id = id.TrimEnd('-');
                    if (!choices.TryGetValue(id, out var maps)) choices[id] = maps = [];
                    if (maps.TryGetValue(original, out var existing) && existing != candidate)
                    {
                        variantErrors.Add(new { item = item.id, variant = id, source = original, reason = $"Ambiguous variant textures: '{existing}', '{candidate}'" });
                        maps[original] = "";
                    }
                    else maps[original] = candidate;
                }
            }
            foreach (var (id, maps) in choices)
            {
                if (maps.Values.Any(string.IsNullOrEmpty)) continue;
                try
                {
                    foreach (var path in maps.Values) EnsureTexture(path);
                    item.variants.Add(new(id, "Variant " + id, maps));
                }
                catch (Exception ex) when (Recoverable(ex)) { variantErrors.Add(new { item = item.id, variant = id, reason = ex.Message }); }
            }
        }
        void WriteJson(string file, object value)
        {
            var target = Path.Combine(output, file);
            File.WriteAllText(target + ".tmp", JsonSerializer.Serialize(value, Options));
            File.Move(target + ".tmp", target, true);
        }
    }
    static string StableId(string path) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..24];
    static bool Recoverable(Exception ex) => ex is not OutOfMemoryException and not OperationCanceledException;
}
