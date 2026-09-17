using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using S4League.Scn;

static class VerifyBundle
{
    public static void Run(ZipArchive archive, string output, string stem = "station2")
    {
        var entries = archive.Entries.ToDictionary(e => e.FullName.ToLowerInvariant());
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, stem + ".json")));
        var manifest = document.RootElement;
        // Lazy scenes own their binary buffer; offsets never address another scene.
        var lazy = manifest.GetProperty("scenes").ValueKind == JsonValueKind.Object;
        var nodes = 0;
        var models = 0;
        var floats = 0;
        var indices = 0;
        var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
        foreach (var (source, buffer) in ReadScenes())
        {
            var key = "game/" + source.GetProperty("source").GetString()!.ToLowerInvariant();
            using var data = new MemoryStream();
            using (var entry = entries[key].Open()) entry.CopyTo(data);
            data.Position = 0;
            var scene = SceneContainer.ReadFrom(data);
            Check(data.Position == data.Length, key + ": source parser consumption");
            var exported = source.GetProperty("nodes");
            Check(exported.GetArrayLength() == scene.Count, key + ": node count");
            Check(source.GetProperty("matrix").EnumerateArray().Select(v => v.GetSingle()).SequenceEqual(Matrix(scene.Header.Matrix)), key + ": root matrix");
            for (var n = 0; n < scene.Count; n++)
            {
                var node = exported[n];
                var original = scene[n];
                nodes++;
                Check(node.GetProperty("name").GetString() == original.Name, key + ": node name");
                Check(node.GetProperty("parent").GetString() == original.SubName, key + ": parent");
                Check(node.GetProperty("matrix").EnumerateArray().Select(v => v.GetSingle()).SequenceEqual(Matrix(original.Matrix)), original.Name + ": matrix");
                if (original is BoneChunk bone && source.GetProperty("role").GetString() != "skeleton")
                    Check(JsonElement.DeepEquals(node.GetProperty("details").GetProperty("animations"), JsonSerializer.SerializeToElement(bone.Animation, jsonOptions)), original.Name + ": bone animations");
                if (original is not ModelChunk model) continue;
                models++;
                var geometry = node.GetProperty("geometry");
                var mesh = model.Mesh;
                Compare("positions", mesh.Vertices.SelectMany(Vector));
                Compare("normals", mesh.Normals.SelectMany(Vector));
                Compare("uv", mesh.UV.SelectMany(v => new[] { v.X, v.Y }));
                Compare("uv1", mesh.UV2.SelectMany(v => new[] { v.X, v.Y }));
                Compare("tangents", mesh.Tangents.SelectMany(Vector));
                var index = geometry.GetProperty("indices");
                Check(index.GetProperty("count").GetInt32() == mesh.Faces.Count * 3, original.Name + ": index count");
                var offset = index.GetProperty("byteOffset").GetInt32();
                foreach (var value in mesh.Triangles())
                {
                    Check(BitConverter.ToUInt32(buffer, offset) == (uint)value, original.Name + ": triangle index");
                    offset += 4; indices++;
                }
                var groups = geometry.GetProperty("groups");
                Check(groups.GetArrayLength() == model.TextureData.Textures.Count, original.Name + ": material groups");
                for (var t = 0; t < groups.GetArrayLength(); t++)
                {
                    var originalGroup = model.TextureData.Textures[t];
                    Check(groups[t].GetProperty("start").GetInt32() == originalGroup.face_offset * 3, "Group start");
                    Check(groups[t].GetProperty("count").GetInt32() == originalGroup.face_count * 3, "Group count");
                    Check(groups[t].GetProperty("sourceMap").GetString() == originalGroup.main_texture, "Diffuse reference");
                    Check(groups[t].GetProperty("sourceLightMap").GetString() == originalGroup.side_texture, "Lightmap reference");
                }
                var details = node.GetProperty("details");
                Check(details.GetProperty("flags").GetInt32() == (int)model.Shader, original.Name + ": flags");
                Check(JsonElement.DeepEquals(details.GetProperty("animations"), JsonSerializer.SerializeToElement(model.Animation, jsonOptions)), original.Name + ": animations");
                Check(JsonElement.DeepEquals(details.GetProperty("bones"), JsonSerializer.SerializeToElement(model.WeightBone, jsonOptions)), original.Name + ": skin weights and inverse binds");
                void Compare(string name, IEnumerable<float> values)
                {
                    var field = geometry.GetProperty(name);
                    var location = field.GetProperty("byteOffset").GetInt32();
                    var count = 0;
                    foreach (var value in values)
                    {
                        Check(BitConverter.ToInt32(buffer, location) == BitConverter.SingleToInt32Bits(value), original.Name + ": " + name);
                        location += 4; count++; floats++;
                    }
                    Check(count == field.GetProperty("count").GetInt32() * field.GetProperty("itemSize").GetInt32(), name + ": count");
                }
            }
        }
        var dependencies = 0;
        foreach (var dependency in manifest.GetProperty("dependencies").EnumerateObject())
        {
            using var entry = entries["game/" + dependency.Name].Open();
            var hash = Convert.ToHexStringLower(SHA256.HashData(entry));
            Check(hash == dependency.Value.GetProperty("sha256").GetString(), dependency.Name + ": archive hash");
            using var file = File.OpenRead(Path.Combine(output, dependency.Value.GetProperty("file").GetString()!));
            Check(Convert.ToHexStringLower(SHA256.HashData(file)) == hash, dependency.Name + ": preserved bytes");
            dependencies++;
        }
        Console.WriteLine($"Source verification passed: {nodes} nodes, {models} meshes, {floats} float32 values, {indices} indices, {dependencies} dependency hashes.");

        IEnumerable<(JsonElement Scene, byte[] Buffer)> ReadScenes()
        {
            if (!lazy)
            {
                var buffer = File.ReadAllBytes(Path.Combine(output, stem + ".bin"));
                foreach (var scene in manifest.GetProperty("scenes").EnumerateArray()) yield return (scene, buffer);
                yield break;
            }
            foreach (var pair in manifest.GetProperty("scenes").EnumerateObject())
            {
                using var scene = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, pair.Value.GetProperty("json").GetString()!)));
                Check(scene.RootElement.GetProperty("source").GetString() == pair.Name, pair.Name + ": lazy scene identity");
                var buffer = File.ReadAllBytes(Path.Combine(output, pair.Value.GetProperty("bin").GetString()!));
                yield return (scene.RootElement, buffer);
            }
        }
    }
    static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidDataException("Source verification failed: " + message);
    }
    static float[] Vector(Vector3 v) => [v.X, v.Y, v.Z];
    static float[] Matrix(Matrix4x4 m) => [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
}
