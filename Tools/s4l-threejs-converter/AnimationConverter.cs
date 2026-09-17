using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using S4League.Scn;

// Animation data stays in the source rig's LOCAL, Y-up coordinate system.
static class AnimationConverter
{
    public const string MainScene = "resources/model/character/female_bip.scn";
    public const string VariantScene = "resources/model/character/bip_female/female_bip_0000.scn";
    const string SocialConfig = "language/_eu_default_option.x7";
    const string SocialStrings = "language/xml/default_option_string_table.x7";
    const string PreviewScript = "resources/script/previewactoranimsetting.lua";
    const string RunScript = "resources/script/actorstates_runstates.lua";
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal record Track(string name, string type, double[] times, float[] values, int interpolation = 2301);
    internal record Clip(string name, double duration, List<Track> tracks, string uuid);
    record Request(string id, string label, string category, string sourceClip, bool loop, bool inPlace, object mapping);
    record Pack(object index, Dictionary<string, object> files, Dictionary<string, byte[]> sources);

    public static void Run(Dictionary<string, ZipArchiveEntry> entries, string output, string archive)
    {
        var pack = Build(entries, archive);
        foreach (var (path, bytes) in pack.sources)
        {
            var target = Path.Combine(output, "source", path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, bytes);
        }
        foreach (var (path, data) in pack.files.Append(new KeyValuePair<string, object>("index.json", pack.index)))
        {
            var target = Path.Combine(output, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, JsonSerializer.Serialize(data, Json));
        }
        Console.WriteLine(JsonSerializer.Serialize(new { output, clips = pack.files.Values.OfType<Clip>().Count() }));
    }

    public static void Verify(Dictionary<string, ZipArchiveEntry> entries, string output, string archive)
    {
        var expected = Build(entries, archive);
        foreach (var (path, data) in expected.files.Append(new KeyValuePair<string, object>("index.json", expected.index)))
        {
            var target = Path.Combine(output, path);
            if (!File.Exists(target) || !JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(target)), JsonSerializer.SerializeToNode(data, Json)))
                throw new InvalidDataException($"Animation source mismatch: {path}");
        }
        foreach (var (path, bytes) in expected.sources)
        {
            var target = Path.Combine(output, "source", path);
            if (!File.Exists(target) || !File.ReadAllBytes(target).SequenceEqual(bytes))
                throw new InvalidDataException($"Animation source dependency mismatch: {path}");
        }
        Console.WriteLine(JsonSerializer.Serialize(new { verified = true,
            clips = expected.files.Values.OfType<Clip>().Count(),
            tracks = expected.files.Values.OfType<Clip>().Sum(c => c.tracks.Count),
            keys = expected.files.Values.OfType<Clip>().Sum(c => c.tracks.Sum(t => t.times.Length)),
            dependencies = expected.sources.Count }));
    }

    static Pack Build(Dictionary<string, ZipArchiveEntry> entries, string archive)
    {
        var sources = new Dictionary<string, byte[]>();
        foreach (var path in new[] { MainScene, VariantScene, SocialConfig, SocialStrings, PreviewScript, RunScript })
        {
            using var source = entries[path].Open(); using var stream = new MemoryStream(); source.CopyTo(stream);
            sources.Add(path, stream.ToArray());
        }
        var libraries = new Dictionary<string, SceneContainer>();
        var libraryEvidence = new List<object>();
        foreach (var path in new[] { MainScene, VariantScene })
        {
            using var stream = new MemoryStream(sources[path]);
            var scene = SceneContainer.ReadFrom(stream);
            if (stream.Position != stream.Length) throw new InvalidDataException($"Trailing SCN bytes: {path}");
            libraries.Add(path, scene);
            libraryEvidence.Add(new { sourceScene = path, bytes = stream.Length, consumedBytes = stream.Position,
                bones = scene.Bones.Count, sourceClips = scene.Bones.SelectMany(b => b.Animation.Select(a => a.Name)).Distinct().Order().ToArray() });
        }
        var preview = Lua51Mapping.ReadCall(sources[PreviewScript], "PreviewActorAnimSetting", "SetFemaleDefaultAnim");
        var run = Lua51Mapping.ReadCall(sources[RunScript], "RunState_WeaponUnused", "SetAnim");
        var runLower = Lua51Mapping.ReadCall(sources[RunScript], "RunState_WeaponUnused", "SetAnim", 8);
        // Fail rather than quietly relabel a different version's action.
        if (preview.arguments[0]?.ToString() != "00029" || run.arguments[0]?.ToString() != "00008" || runLower.arguments[0]?.ToString() != "00008")
            throw new InvalidDataException("Original movement mapping changed; review it explicitly");
        var requests = new List<Request> {
            new("idle", "Standing idle", "Movement", "00029", true, false, new { source = PreviewScript, call = preview }),
            new("walk", "Walking / running (unarmed)", "Movement", "00008", true, true, new { source = RunScript, call = run, lowerCall = runLower,
                note = "Original forward WeaponUnused locomotion, named RunState in the game. Raw authored timing retained; not an armed walk or a synthesized slow walk." })
        };
        using var textStream = new MemoryStream(sources[SocialStrings]);
        var strings = XDocument.Load(textStream).Descendants("string").ToDictionary(e => (string)e.Attribute("key")!, e => (string?)e.Attribute("eng") ?? "");
        using var configStream = new MemoryStream(sources[SocialConfig]);
        foreach (var data in XDocument.Load(configStream).Descendants("data").Where(e => !string.IsNullOrEmpty((string?)e.Attribute("animation")) && (string?)e.Attribute("useweapon") == "false"))
        {
            var clip = (string)data.Attribute("animation")!;
            var labelKey = (string)data.Attribute("text_key")!;
            var commandKey = (string)data.Attribute("key_key")!;
            var id = clip switch { "O0003" => "greet", "O0007" => "cry", "O0015" => "scissors", "O0016" => "rock", "O0017" => "paper", "O0018" => "wave", _ => "emote-" + clip.ToLowerInvariant() };
            requests.Add(new(id, strings[labelKey], "Emotes", clip, false, false,
                new { source = SocialConfig, sourceStrings = SocialStrings, socialId = (string?)data.Attribute("id"), labelKey, commandKey,
                    sourceLabel = strings[labelKey], sourceCommand = strings[commandKey], useWeapon = false }));
        }
        var files = new Dictionary<string, object>();
        var clips = new List<object>();
        var unavailable = new List<object>();
        var evidence = new Dictionary<string, object>();
        var sourceKeys = 0; var staticChannels = 0; var fallbackBones = 0; var copiedBones = 0;
        foreach (var request in requests)
        {
            var scenePath = libraries[MainScene].Bones.Any(b => b.Animation.Any(a => a.Name == request.sourceClip))
                ? MainScene : VariantScene;
            var sceneMain = libraries[scenePath];
            if (!sceneMain.Bones.Any(b => b.Animation.Any(a => a.Name == request.sourceClip)))
            {
                unavailable.Add(new { request.id, request.label, request.sourceClip, sourceScene = scenePath, request.mapping,
                    reason = "Mapped social clip is absent from the selected female library; no substitute exported.",
                    otherLibrariesContainingClip = libraries.Where(p => p.Value.Bones.Any(b => b.Animation.Any(a => a.Name == request.sourceClip))).Select(p => p.Key).ToArray() });
                continue;
            }
            var clip = Export(sceneMain, request.sourceClip, request.id, libraries, scenePath);
            var perBone = new List<object>();
            foreach (var bone in sceneMain.Bones)
            {
                var animation = bone.Animation.SingleOrDefault(a => a.Name == request.sourceClip);
                var chain = new HashSet<string>();
                var data = ResolveLibraries(libraries, scenePath, bone.Name, request.sourceClip, chain);
                var key = data.TransformKey ?? bone.Animation.Single(a => a.Name == "BASE").TransformKeyData!.TransformKey!;
                var count = key.TKey.Count + key.RKey.Count + key.SKey.Count;
                sourceKeys += count;
                staticChannels += (key.TKey.Count == 0 ? 1 : 0) + (key.RKey.Count == 0 ? 1 : 0) + (key.SKey.Count == 0 ? 1 : 0);
                if (animation is null || data.TransformKey is null) fallbackBones++;
                if (!string.IsNullOrWhiteSpace(animation?.Copy)) copiedBones++;
                perBone.Add(new { bone = bone.Name, sourceClip = animation?.Name ?? "BASE", copyChain = chain.ToArray(),
                    baseFallback = animation is null || data.TransformKey is null, sourceDuration = data.Duration.TotalSeconds,
                    baseTranslation = V(key.Translation), baseRotation = Q(key.Rotation), baseScale = V(key.Scale),
                    sourceTranslationKeys = key.TKey.Count, sourceRotationKeys = key.RKey.Count, sourceScaleKeys = key.SKey.Count,
                    floatKeys = data.FloatKeys.Select(f => new { time = f.Duration.TotalSeconds, alpha = f.Alpha }).ToArray() });
            }
            var url = "clips/" + request.id + ".json";
            files.Add(url, clip); evidence.Add(request.id, perBone);
            clips.Add(new { request.id, request.label, request.category, url, request.loop, request.sourceClip, sourceScene = scenePath,
                clip.duration, rootBone = "Bip01", request.inPlace, request.mapping,
                trackCount = clip.tracks.Count, keyCount = clip.tracks.Sum(t => t.times.Length) });
        }
        files.Add("provenance/bone-channels.json", evidence);
        var copyAudit = new List<object>();
        var copyFailures = new List<object>();
        foreach (var (scenePath, library) in libraries)
            foreach (var bone in library.Bones)
                foreach (var animation in bone.Animation.Where(a => !string.IsNullOrWhiteSpace(a.Copy)))
                {
                    var chain = new HashSet<string>();
                    try
                    {
                        var data = ResolveLibraries(libraries, scenePath, bone.Name, animation.Name, chain);
                        copyAudit.Add(new { sourceScene = scenePath, bone = bone.Name, sourceClip = animation.Name, copy = animation.Copy,
                            chain = chain.ToArray(), sourceDuration = data.Duration.TotalSeconds,
                            sourceTrsKeys = data.TransformKey is { } key ? key.TKey.Count + key.RKey.Count + key.SKey.Count : 0,
                            baseFallback = chain.Last().EndsWith("::BASE", StringComparison.Ordinal) });
                    }
                    catch (InvalidDataException exception)
                    {
                        copyFailures.Add(new { sourceScene = scenePath, bone = bone.Name, sourceClip = animation.Name,
                            copy = animation.Copy, chain = chain.ToArray(), reason = exception.Message });
                    }
                }
        files.Add("provenance/copy-audit.json", new { resolved = copyAudit, unresolved = copyFailures });
        var dependencies = sources.ToDictionary(p => p.Key, p => new { file = "source/" + p.Key, bytes = p.Value.Length,
            sha256 = Convert.ToHexStringLower(SHA256.HashData(p.Value)) });
        var index = new { format = "s4-character-animations", version = 1, bodyId = "female", sourceArchive = archive, clips, unavailable,
            coordinates = "Original local TRS, original S4 units, Y up. Bone names and quaternion components are unchanged; apply the actor's handedness transform only once.",
            rootMotion = new { exported = "raw", rootBone = "Bip01", horizontalAxes = new[] { "x", "z" },
                treatment = "inPlace is a preview recommendation, not baked conversion. Countertranslate a separate actor placement root by Bip01's current minus clip-start x/z displacement after mixer evaluation; leave tracked bones, y and quaternions untouched. Never accumulate offsets." },
            provenance = new { selection = "Female preview default and RunState_WeaponUnused forward locomotion decoded from original Lua 5.1 bytecode; social actions and English labels joined from original XML.",
                selectedLibrary = MainScene, reason = "Main library contains both movement mappings and older social clips. O0034 is present only in the variant library; it is exported from that exact source. Shared names prefer the canonical main library, not arbitrary replacement.",
                boneChannels = "provenance/bone-channels.json", copyAudit = "provenance/copy-audit.json", libraries = libraryEvidence },
            dependencies,
            verification = new { fullyConsumedScenes = libraries.Count, selectedClips = clips.Count, unavailableClips = unavailable.Count,
                tracks = files.Values.OfType<Clip>().Sum(c => c.tracks.Count), sourceTrsKeys = sourceKeys, staticChannels, baseFallbackBones = fallbackBones,
                selectedCopiedBones = copiedBones, unresolvedSelectedCopies = 0,
                libraryCopyRecords = copyAudit.Count + copyFailures.Count, resolvedLibraryCopies = copyAudit.Count, unresolvedLibraryCopies = copyFailures.Count,
                exportedKeys = files.Values.OfType<Clip>().Sum(c => c.tracks.Sum(t => t.times.Length)),
                unsupported = "Float/alpha channels are preserved in bone provenance, not mapped to skeletal TRS. No sound, particles or facial mesh morphs are exported." } };
        return new(index, files, sources);
    }

    internal static Clip Export(SceneContainer scene, string sourceClip, string id,
        Dictionary<string, SceneContainer>? libraries = null, string scenePath = MainScene)
    {
        if (!scene.Bones.Any(b => b.Animation.Any(a => a.Name == sourceClip))) throw new InvalidDataException($"Missing source clip {sourceClip}");
        var tracks = new List<Track>();
        double duration = 0;
        foreach (var bone in scene.Bones)
        {
            var data = ResolveLibraries(libraries ?? new() { [scenePath] = scene }, scenePath, bone.Name, sourceClip, new HashSet<string>());
            var key = data.TransformKey ?? bone.Animation.Single(a => a.Name == "BASE").TransformKeyData!.TransformKey!;
            duration = Math.Max(duration, data.Duration.TotalSeconds);
            tracks.Add(Channel(bone.Name + ".position", "vector", key.TKey.Select(k => (k.Duration.TotalSeconds, V(k.Translation))).ToArray(), V(key.Translation)));
            tracks.Add(Channel(bone.Name + ".quaternion", "quaternion", key.RKey.Select(k => (k.Duration.TotalSeconds, Q(k.Rotation))).ToArray(), Q(key.Rotation)));
            tracks.Add(Channel(bone.Name + ".scale", "vector", key.SKey.Select(k => (k.Duration.TotalSeconds, V(k.Scale))).ToArray(), V(key.Scale)));
        }
        if (!double.IsFinite(duration) || duration < 0 || tracks.Any(t => t.times[^1] > duration))
            throw new InvalidDataException($"Keys outside source duration: {sourceClip}");
        // Clip identity is not a bone binding. AnimationClip.parse overwrites its
        // generated UUID with json.uuid; omitting it aliases every mixer cache key.
        // RFC 9562 UUIDv8: stable SHA-256-derived source-library/clip identity.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("s4-character-animations/v1/" + scenePath + "/" + sourceClip));
        hash[6] = (byte)((hash[6] & 15) | 128); hash[8] = (byte)((hash[8] & 63) | 128);
        var hex = Convert.ToHexStringLower(hash.AsSpan(0, 16));
        var uuid = $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..32]}";
        return new(id, duration, tracks, uuid);
    }
    internal static TransformKeyData Resolve(SceneContainer scene, string boneName, string clip, HashSet<string> chain)
        => ResolveLibraries(new() { [MainScene] = scene }, MainScene, boneName, clip, chain);

    internal static TransformKeyData ResolveLibraries(Dictionary<string, SceneContainer> libraries,
        string scenePath, string boneName, string clip, HashSet<string> chain)
    {
        if (!chain.Add(scenePath + "::" + clip)) throw new InvalidDataException($"Animation copy cycle: {boneName}/{clip}");
        var scene = libraries[scenePath];
        if (!scene.Bones.Any(b => b.Animation.Any(a => a.Name == clip)))
        {
            var candidates = libraries.Where(p => p.Key != scenePath && p.Value.Bones.Any(b => b.Animation.Any(a => a.Name == clip))).ToArray();
            if (candidates.Length != 1)
                throw new InvalidDataException($"Missing or ambiguous animation copy: {boneName}/{clip} ({candidates.Length} external libraries)");
            return ResolveLibraries(libraries, candidates[0].Key, boneName, clip, chain);
        }
        var bone = scene.Bones.SingleOrDefault(b => b.Name == boneName)
            ?? throw new InvalidDataException($"Missing source bone: {scenePath}/{boneName}");
        var animation = bone.Animation.SingleOrDefault(a => a.Name == clip);
        if (animation is null)
        {
            // The source CLIP exists, but has no channel for this bone: its BASE local
            // transform is the correct sparse-animation fallback, including copied clips.
            if (clip == "BASE") throw new InvalidDataException($"Missing BASE: {boneName}");
            return ResolveLibraries(libraries, scenePath, boneName, "BASE", chain);
        }
        if (!string.IsNullOrWhiteSpace(animation.Copy)) return ResolveLibraries(libraries, scenePath, boneName, animation.Copy, chain);
        return animation.TransformKeyData ?? throw new InvalidDataException($"Missing animation data: {boneName}/{clip}");
    }
    internal static Track Channel(string name, string type, (double time, float[] values)[] keys, float[] fallback)
    {
        var size = type == "quaternion" ? 4 : 3;
        if (fallback.Length != size || fallback.Any(v => !float.IsFinite(v))) throw new InvalidDataException($"Invalid static values: {name}");
        double previous = -1;
        foreach (var (time, values) in keys)
        {
            if (!double.IsFinite(time) || time < 0 || time <= previous) throw new InvalidDataException($"Unsorted or duplicate key times: {name}");
            if (values.Length != size || values.Any(v => !float.IsFinite(v))) throw new InvalidDataException($"Invalid values: {name}");
            previous = time;
        }
        return keys.Length == 0 ? new(name, type, [0], fallback) : new(name, type, keys.Select(k => k.time).ToArray(), keys.SelectMany(k => k.values).ToArray());
    }
    internal static float[] V(Vector3 v) => [v.X, v.Y, v.Z];
    internal static float[] Q(Quaternion q) => [q.X, q.Y, q.Z, q.W];
}

// Static bytecode reader, NOT a Lua VM: never executes supplied scripts.
static class Lua51Mapping
{
    internal record Call(string function, string method, int instruction, object?[] arguments);
    sealed record Proto(uint[] code, object?[] constants, Proto[] children);
    internal static Call ReadCall(byte[] bytes, string function, string method, int occurrence = 0)
    {
        using var stream = new MemoryStream(bytes); using var reader = new BinaryReader(stream);
        byte[] expected = [27, 76, 117, 97, 81, 0, 1, 4, 4, 4, 8, 0];
        if (!reader.ReadBytes(12).SequenceEqual(expected)) throw new InvalidDataException("Unsupported Lua bytecode header");
        var root = ReadProto(reader);
        if (stream.Position != stream.Length) throw new InvalidDataException("Trailing Lua bytes");
        Proto? target = null;
        for (int i = 0; i + 1 < root.code.Length; i++)
            if ((root.code[i] & 63) == 36 && (root.code[i + 1] & 63) == 7 && root.constants[root.code[i + 1] >> 14]?.ToString() == function)
                target = root.children[root.code[i] >> 14];
        if (target is null) throw new InvalidDataException($"Lua function missing: {function}");
        // A SELF followed by constant/global argument loads and CALL is sufficient for
        // the two mapping functions. Other expressions are rejected, never guessed.
        for (int i = 0; i < target.code.Length; i++)
        {
            var code = target.code[i]; int a = (int)(code >> 6) & 255, c = (int)(code >> 14) & 511;
            if ((code & 63) != 11 || c < 256 || target.constants[c - 256]?.ToString() != method) continue;
            var registers = new Dictionary<int, object?>();
            for (int j = i + 1; j < target.code.Length; j++)
            {
                code = target.code[j]; int op = (int)(code & 63), reg = (int)(code >> 6) & 255;
                if (op is 1 or 5) registers[reg] = target.constants[code >> 14];
                else if (op == 28 && reg == a)
                {
                    int count = (int)(code >> 23) - 2;
                    if (occurrence-- > 0) break;
                    return new(function, method, j, Enumerable.Range(a + 2, count).Select(r => registers.TryGetValue(r, out var value) ? value : throw new InvalidDataException("Nonconstant Lua mapping argument")).ToArray());
                }
                else break;
            }
        }
        throw new InvalidDataException($"Lua mapping call missing: {function}/{method}");
    }
    static string ReadString(BinaryReader r)
    {
        int length = checked((int)r.ReadUInt32());
        var bytes = r.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException();
        return Encoding.UTF8.GetString(length == 0 ? bytes : bytes[..^1]);
    }
    static Proto ReadProto(BinaryReader r)
    {
        ReadString(r); r.ReadUInt32(); r.ReadUInt32(); r.ReadBytes(4);
        var code = Enumerable.Range(0, r.ReadInt32()).Select(_ => r.ReadUInt32()).ToArray();
        var constants = new object?[r.ReadInt32()];
        for (int i = 0; i < constants.Length; i++) constants[i] = r.ReadByte() switch {
            0 => null, 1 => r.ReadByte() != 0, 3 => r.ReadDouble(), 4 => ReadString(r), _ => throw new InvalidDataException("Unsupported Lua constant") };
        var children = Enumerable.Range(0, r.ReadInt32()).Select(_ => ReadProto(r)).ToArray();
        var lines = r.ReadInt32(); for (int i = 0; i < lines; i++) r.ReadUInt32();
        var locals = r.ReadInt32(); for (int i = 0; i < locals; i++) { ReadString(r); r.ReadUInt32(); r.ReadUInt32(); }
        var upvalues = r.ReadInt32(); for (int i = 0; i < upvalues; i++) ReadString(r);
        return new(code, constants, children);
    }
}
