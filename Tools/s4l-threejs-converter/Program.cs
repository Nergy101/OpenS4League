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
var rigArg = args.SkipWhile(a => a != "--rig").Skip(1).FirstOrDefault();
var qualityArg = args.SkipWhile(a => a != "--texture-quality").Skip(1).FirstOrDefault();
var algorithm = args.SkipWhile(a => a != "--upscale-algorithm").Skip(1).FirstOrDefault() ?? "deterministic-bilinear";
if (args.Length < 2 || (animationMode ? wardrobeMode || characterMode : wardrobeMode && characterMode) || (characterMode && args.Length < 4) || (!characterMode && !wardrobeMode && !animationMode && mapArg is null))
{
    Console.Error.WriteLine("Usage: dotnet run --project Tools/s4l-threejs-converter -- <Season-8-client.zip> <output-directory> --map <Tools/s4l-threejs-converter/maps/<map>.json> [--texture-quality 1x,4x] [--upscale-algorithm deterministic-bilinear] [--verify]");
    Console.Error.WriteLine("   or: dotnet run --project Tools/s4l-threejs-converter -- <Season-8-client.zip> <output-directory> (--character <recipe.json> | --wardrobe [--rig <id>] | --animations [--rig female|male]) [--texture-quality 1x,4x] [--upscale-algorithm deterministic-bilinear] [--verify]");
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
    if (verify) AnimationConverter.Verify(entries, output, Path.GetFileName(args[0]), AnimationConverter.ResolveRig(rigArg));
    else AnimationConverter.Run(entries, output, Path.GetFileName(args[0]), AnimationConverter.ResolveRig(rigArg));
    return 0;
}
if (wardrobeMode)
{
    if (verify) VerifyBundle.Run(archive, output, "index");
    else WardrobeConverter.Run(entries, output, Path.GetFileName(args[0]), qualities, algorithm, rigArg);
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
// A texture bound to a material's lightMap slot is baked lighting; the scanner also reports the
// name pattern alone, because a normal map is never diffuse colour either.
var sideTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
// The map bundle carries the wardrobe's variant contract: 1x is the decoded original, 4x the
// Real-ESRGAN-enhanced level, and `kind` says what may be upscaled at all — baked lighting and
// normal vectors are semantic data, so they are never generated.
var textureQualities = qualities ?? ["1x"];
var previousTextures = LoadPreviousTextures();
var textures = new Dictionary<string, object>();
foreach (var path in texturePaths.Order())
{
    var kind = sideTextures.Contains(path) ? "lightmap"
        : Regex.IsMatch(path, @"(?:_n|normal)\.(dds|tga|bmp|png)$", RegexOptions.IgnoreCase) ? "normal" : "color";
    var source = Read(entries[path]);
    var sourceSha = Convert.ToHexStringLower(SHA256.HashData(source));
    var decoded = PngTexture.Decode(source);
    var variants = new Dictionary<string, object>();
    foreach (var quality in textureQualities)
    {
        var scale = quality switch { "1x" => 1, "4x" => 4, _ => throw new ArgumentException($"Unknown texture quality '{quality}': this build generates 1x and 4x (2x and 8x were dropped).") };
        var file = "textures/" + path + "." + quality + ".png";
        var destination = Path.Combine(output, file);
        // Reuse a level that is already on disk and still matches its recorded hashes — including a
        // variant another generator produced (the ESRGAN pass) — instead of re-encoding it.
        if (previousTextures.TryGetValue(path, out var previous) && previous.TryGetValue(quality, out var kept)
            && kept.file == file && kept.sourceSha256 == sourceSha
            && kept.width == decoded.Width * scale && kept.height == decoded.Height * scale
            && File.Exists(destination) && PngTexture.Sha256(destination) == kept.generatedSha256)
        {
            variants[quality] = new { file = kept.file, width = kept.width, height = kept.height, algorithm = kept.algorithm,
                sourceWidth = decoded.Width, sourceHeight = decoded.Height, sourceSha256 = sourceSha,
                generatedSha256 = kept.generatedSha256, derivedFromSha256 = kept.derivedFromSha256 };
            continue;
        }
        PngTexture.Write(PngTexture.Resize(decoded, scale), destination);
        variants[quality] = new { file, width = decoded.Width * scale, height = decoded.Height * scale, algorithm,
            sourceWidth = decoded.Width, sourceHeight = decoded.Height, sourceSha256 = sourceSha,
            generatedSha256 = PngTexture.Sha256(destination), derivedFromSha256 = (string?)null };
    }
    textures[path] = new { kind, variants };
}
var manifest = new
{
    format = bundleFormat, version = 1, name = mapName,
    catalog,
    sourceArchive = Path.GetFileName(args[0]), sourceConfig = configEntry.FullName[5..],
    coordinates = "Original S4 units, Y up; row-vector matrices serialized for Three.js column-major arrays. Loader reflects Z once for left-to-right handedness.",
    buffer = stem + ".bin", scenes, textures, texturePaths = texturePaths.Order().ToArray(), aliases,
    coverage = new { textureQuality = new { requested = textureQualities, algorithm } },
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
    sideTextures.UnionWith(result.SideTextures);
    if (catalog is not null) catalog.SceneTextures[entry.FullName[5..].ToLowerInvariant()] = result.DiffuseTextures;
}
// What a previous conversion of this same map recorded for each texture, so a re-conversion can
// reuse levels that are already on disk — including the ESRGAN-generated 4x ones.
Dictionary<string, Dictionary<string, MapVariant>> LoadPreviousTextures()
{
    var path = Path.Combine(output, stem + ".json");
    if (!File.Exists(path)) return new(StringComparer.Ordinal);
    try
    {
        var previous = JsonSerializer.Deserialize<PreviousMap>(File.ReadAllText(path), options);
        var result = new Dictionary<string, Dictionary<string, MapVariant>>(StringComparer.Ordinal);
        foreach (var (key, value) in previous?.textures ?? new Dictionary<string, MapTextures>())
            if (value?.variants is { Count: > 0 }) result[key] = value.variants;
        return result;
    }
    catch (JsonException)
    {
        // A manifest that cannot be read is not a reason to fail: every level is encoded again.
        return new(StringComparer.Ordinal);
    }
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

/// <summary>One recorded texture level from a previous conversion of the same bundle.</summary>
sealed class MapVariant
{
    public string file { get; set; } = "";
    public int width { get; set; }
    public int height { get; set; }
    public string? algorithm { get; set; }
    public string? sourceSha256 { get; set; }
    public string? generatedSha256 { get; set; }
    public string? derivedFromSha256 { get; set; }
}

/// <summary>A texture of a previous conversion: its semantic kind and the levels it recorded.</summary>
sealed class MapTextures
{
    public string? kind { get; set; }
    public Dictionary<string, MapVariant>? variants { get; set; }
}

/// <summary>The parts of a previously written map manifest this converter reads back.</summary>
sealed class PreviousMap
{
    public Dictionary<string, MapTextures> textures { get; set; } = new(StringComparer.Ordinal);
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
