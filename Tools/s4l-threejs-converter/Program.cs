using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using S4League.Scn;

var animationMode = args.Contains("--animations");
var wardrobeMode = args.Contains("--wardrobe");
var characterMode = args.Contains("--character");
var verify = args.Contains("--verify");
var mapArg = args.SkipWhile(a => a != "--map").Skip(1).FirstOrDefault();
var qualityArg = args.SkipWhile(a => a != "--texture-quality").Skip(1).FirstOrDefault();
var algorithm = args.SkipWhile(a => a != "--upscale-algorithm").Skip(1).FirstOrDefault() ?? "deterministic-bilinear";
if (args.Length < 2 || (animationMode ? wardrobeMode || characterMode : wardrobeMode && characterMode) || (characterMode && args.Length < 4) || (!characterMode && !wardrobeMode && !animationMode && mapArg is null))
{
    Console.Error.WriteLine("Usage: dotnet run --project Tools/s4l-threejs-converter -- <Season-8-client.zip> <output-directory> --map <Tools/s4l-threejs-converter/maps/<map>.json> [--texture-quality 1x,2x,4x] [--upscale-algorithm deterministic-bilinear] [--verify]");
    Console.Error.WriteLine("   or: dotnet run --project Tools/s4l-threejs-converter -- <Season-8-client.zip> <output-directory> (--character <recipe.json> | --wardrobe | --animations) [--texture-quality 1x,2x,4x] [--upscale-algorithm deterministic-bilinear] [--verify]");
    return 1;
}
var qualities = qualityArg?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
using var archive = ZipFile.OpenRead(args[0]);
var entries = archive.Entries.Where(e => e.FullName.StartsWith("Game/", StringComparison.OrdinalIgnoreCase)
    && !e.FullName.EndsWith('/')).ToDictionary(e => e.FullName[5..].ToLowerInvariant());
var output = Path.GetFullPath(args[1]);
if (animationMode)
{
    if (verify) AnimationConverter.Verify(entries, output, Path.GetFileName(args[0]));
    else AnimationConverter.Run(entries, output, Path.GetFileName(args[0]));
    return 0;
}
if (wardrobeMode)
{
    if (verify) VerifyBundle.Run(archive, output, "index");
    else WardrobeConverter.Run(entries, output, Path.GetFileName(args[0]), qualities, algorithm);
    return 0;
}
var recipe = characterMode ? null : MapRecipe.Load(mapArg!);
var stem = characterMode ? "character" : recipe!.bundle;
var bundleFormat = characterMode ? "s4-character-threejs" : "s4-map-threejs";
if (verify)
{
    VerifyBundle.Run(archive, output, stem);
    return 0;
}
Directory.CreateDirectory(output);
var assets = new ConversionAssets(entries, output);
var dependencies = assets.dependencies;
var aliases = assets.aliases;
var missing = assets.missing;
var scenes = new List<object>();
var texturePaths = assets.texturePaths;
var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
var catalog = characterMode ? CharacterCatalog.Load(args[3], entries) : null;
var mapName = catalog?.name ?? recipe!.name;
var configPath = characterMode ? "xml/default_item.x7" : recipe!.config;
if (!entries.TryGetValue(configPath, out var configEntry)) throw new FileNotFoundException($"Map configuration not found in the client ZIP: {configPath}");
var config = characterMode ? new Dictionary<string, Dictionary<string, string>>() : ParseIni(Encoding.GetEncoding(949).GetString(Read(configEntry)));
Copy(configEntry);
if (catalog is not null)
{
    Copy(entries["xml/item.x7"]);
    Copy(entries["language/xml/iteminfo_string_table.x7"]);
}
else File.WriteAllText(Path.Combine(output, "map-config.json"), JsonSerializer.Serialize(config, options));
using var binary = new BinaryWriter(File.Create(Path.Combine(output, stem + ".bin")));
var totalVertices = 0;
var totalTriangles = 0;
var totalModels = 0;
if (catalog is not null)
    foreach (var request in catalog.Scenes.Distinct()) ExportScene(entries[request.Path], request.Role);
foreach (var role in new[] { "SKY", "STATIC", "DYNAMIC", "GAMERULE" })
{
    if (!config.TryGetValue(role, out var section)) continue;
    foreach (var reference in section.Values.Where(v => v.EndsWith(".scn", StringComparison.OrdinalIgnoreCase)))
    {
        // A reference this client build does not ship is recorded like any other unresolved
        // dependency. Nothing is substituted for it, and a map that resolves no scene at all
        // still fails.
        var entry = Resolve(reference, "resources/model/background/");
        if (entry is null) { missing.Add(reference); continue; }
        ExportScene(entry, role.ToLowerInvariant());
    }
}
if (scenes.Count == 0) throw new InvalidDataException($"{mapName}: no scene resolved from {configPath}");
// Preserve direct configuration references, then binary sequence texture dependencies.
foreach (var section in config.Values)
    foreach (var value in section.Values)
        if (Regex.IsMatch(value, @"\.(ini|oct|seq|ogg|wav|dds|tga|bmp|png)$", RegexOptions.IgnoreCase))
            Preserve(value, "resources/model/background/");
if (entries.TryGetValue(configEntry.FullName[5..].ToLowerInvariant() + ".oct", out var octree)) Copy(octree);
// Addon names are effect-system identifiers rather than always being filenames.
foreach (var value in config.GetValueOrDefault("ADDON_GEOM", new()).Values)
{
    var addon = Resolve(value + ".scn", "resources/model/effect/");
    if (addon is not null) ExportScene(addon, "addon");
    else missing.Add("effect identifier: " + value);
}
// Runtime team swaps use the matching enemy texture even though SCN stores _atex.
foreach (var path in texturePaths.Where(p => p.Contains("_atex")).ToArray())
{
    var enemy = path.Replace("_atex", "_etex");
    if (entries.ContainsKey(enemy)) Preserve(enemy, "resources/model/background/");
}
catalog?.ResolveVariants(entries, path => Preserve(path, "resources/model/character/"));
binary.Flush();
var textures = new Dictionary<string, object>();
foreach (var path in texturePaths.Order())
{
    var file = "textures/" + path + ".png";
    var size = PngTexture.Convert(Read(entries[path]), Path.Combine(output, file));
    textures[path] = new { file, width = size.Width, height = size.Height };
}
var manifest = new
{
    format = bundleFormat, version = 1, name = mapName,
    catalog,
    sourceArchive = Path.GetFileName(args[0]), sourceConfig = configEntry.FullName[5..],
    coordinates = "Original S4 units, Y up; row-vector matrices serialized for Three.js column-major arrays. Loader reflects Z once for left-to-right handedness.",
    buffer = stem + ".bin", scenes, textures, texturePaths = texturePaths.Order().ToArray(), aliases,
    totals = new { scenes = scenes.Count, models = totalModels, vertices = totalVertices, triangles = totalTriangles },
    dependencies, unresolved = missing.ToArray()
};
File.WriteAllText(Path.Combine(output, stem + ".json"), JsonSerializer.Serialize(manifest, options));
if (catalog is null) WriteMapsIndex(output, mapName, stem + ".json", bundleFormat);
foreach (var scene in missing.Where(m => m.EndsWith(".scn", StringComparison.OrdinalIgnoreCase)))
    Console.WriteLine($"WARNING: {mapName} references {scene}, which this client build does not ship; skipped and recorded in unresolved.");
Console.WriteLine(JsonSerializer.Serialize(new { manifest.totals, textures = texturePaths.Count, dependencies = dependencies.Count, unresolved = missing }, options));
return 0;

byte[] Read(ZipArchiveEntry entry) => assets.Read(entry);
void Copy(ZipArchiveEntry entry) => assets.Copy(entry);
ZipArchiveEntry? Resolve(string reference, string context) => assets.Resolve(reference, context);
string? Preserve(string reference, string context) => assets.Preserve(reference, context);
void ExportScene(ZipArchiveEntry entry, string role)
{
    Copy(entry);
    var result = SceneExporter.Export(entry, role, binary, Preserve);
    scenes.Add(result.Scene);
    totalModels += result.Models; totalVertices += result.Vertices; totalTriangles += result.Triangles;
    if (catalog is not null) catalog.SceneTextures[entry.FullName[5..].ToLowerInvariant()] = result.DiffuseTextures;
}
// The preview server has no directory listing, so the viewers read this index (written
// next to the converted maps) to find a map's bundle files. It describes just-written
// output; no map data is duplicated into it.
void WriteMapsIndex(string mapDirectory, string name, string manifestFile, string format)
{
    var directory = Path.GetFileName(Path.GetFullPath(mapDirectory));
    var mapsDirectory = new DirectoryInfo(Path.GetFullPath(mapDirectory)).Parent
        ?? throw new InvalidOperationException($"No parent directory for {mapDirectory}");
    var indexPath = Path.Combine(mapsDirectory.FullName, "index.json");
    var index = File.Exists(indexPath)
        ? JsonSerializer.Deserialize<MapsIndex>(File.ReadAllText(indexPath), options) ?? new MapsIndex()
        : new MapsIndex();
    index.maps.RemoveAll(entry => entry.directory == directory);
    index.maps.Add(new MapEntry
    {
        id = Slug(name), name = name, directory = directory, manifest = manifestFile, config = "map-config.json", bundleFormat = format
    });
    index.maps.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
    File.WriteAllText(indexPath, JsonSerializer.Serialize(index, options));
    Console.WriteLine($"Registered map '{Slug(name)}' in {indexPath}");
}

static string Slug(string value)
{
    var characters = new List<char>();
    foreach (var character in value.ToLowerInvariant())
    {
        if (char.IsLetterOrDigit(character)) characters.Add(character);
        else if (characters.Count > 0 && characters[^1] != '-') characters.Add('-');
    }
    while (characters.Count > 0 && characters[^1] == '-') characters.RemoveAt(characters.Count - 1);
    return new string([.. characters]);
}

Dictionary<string, Dictionary<string, string>> ParseIni(string text)
{
    var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, string>? section = null;
    foreach (var raw in text.Split('\n'))
    {
        var line = raw.Split(';')[0].Trim();
        if (line.StartsWith('[') && line.EndsWith(']'))
        {
            var name = line[1..^1];
            if (!result.TryGetValue(name, out section)) result[name] = section = new(StringComparer.OrdinalIgnoreCase);
        }
        else if (section is not null && line.Contains('='))
        {
            var pair = line.Split('=', 2); section[pair[0].Trim()] = pair[1].Trim();
        }
    }
    return result;
}

/// <summary>Shape of Client/Models/Maps/index.json, mirroring the TypeScript-free JS reader.</summary>
sealed class MapsIndex
{
    public string format = "s4-maps-index";
    public int version = 1;
    public List<MapEntry> maps = new();
}

sealed class MapEntry
{
    public string id = "";
    // The display name, so a map picker does not have to download every bundle to name them.
    public string name = "";
    public string directory = "";
    public string manifest = "";
    public string config = "map-config.json";
    public string bundleFormat = "";
}
