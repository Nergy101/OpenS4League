using System.IO.Compression;
using System.Numerics;
using S4League.Scn;

record SceneExport(object Scene, string[] DiffuseTextures, string[] SideTextures, int Models, int Vertices, int Triangles);

// Single serialization path for maps, recipes, and lazy wardrobe scenes.
static class SceneExporter
{
    public static byte[] Read(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var data = new MemoryStream();
        stream.CopyTo(data);
        return data.ToArray();
    }
    public static SceneExport Export(ZipArchiveEntry entry, string role, BinaryWriter binary, Func<string, string, string?> preserve)
    {
        var data = Read(entry);
        var totalModels = 0; var totalVertices = 0; var totalTriangles = 0;
        using var stream = new MemoryStream(data);
        var scene = SceneContainer.ReadFrom(stream);
        if (stream.Position != stream.Length) throw new InvalidDataException($"Parser did not consume {entry.FullName}: {stream.Position}/{stream.Length}");
        var nodes = new List<object>();
        var diffuseTextures = new List<string>();
        // Side surfaces are baked lighting or normals, never diffuse colour: the texture kinds
        // downstream (and the ESRGAN pass) depend on knowing which is which.
        var sideTextures = new List<string>();
        var textureContext = Path.GetDirectoryName(entry.FullName[5..])!.Replace('\\', '/') + "/";
        foreach (var chunk in scene)
        {
            object? geometry = null;
            object? details = null;
            if (chunk is ModelChunk model)
            {
                var mesh = model.Mesh;
                if (mesh.Faces.Any(f => f.X >= mesh.Vertices.Count || f.Y >= mesh.Vertices.Count || f.Z >= mesh.Vertices.Count))
                    throw new InvalidDataException($"Invalid triangle indices: {chunk.Name}");
                var positions = Attribute(mesh.Vertices.SelectMany(Vec), 3);
                var normals = Attribute(mesh.Normals.SelectMany(Vec), 3);
                var uv = Attribute(mesh.UV.SelectMany(v => new[] { v.X, v.Y }), 2);
                var uv1 = Attribute(mesh.UV2.SelectMany(v => new[] { v.X, v.Y }), 2);
                var tangents = Attribute(mesh.Tangents.SelectMany(Vec), 3);
                var indexOffset = binary.BaseStream.Position;
                foreach (var i in mesh.Triangles()) binary.Write((uint)i);
                var groups = model.TextureData.Textures.Select(t => new
                {
                    start = t.face_offset * 3, count = t.face_count * 3,
                    map = string.IsNullOrWhiteSpace(t.main_texture) ? null : preserve(t.main_texture, textureContext),
                    lightMap = string.IsNullOrWhiteSpace(t.side_texture) ? null : preserve(t.side_texture, textureContext),
                    sourceMap = t.main_texture, sourceLightMap = t.side_texture
                }).ToArray();
                diffuseTextures.AddRange(groups.Where(g => g.map is not null).Select(g => g.map!));
                sideTextures.AddRange(groups.Where(g => g.lightMap is not null).Select(g => g.lightMap!));
                geometry = new { positions, normals, uv, uv1, tangents, indices = new { byteOffset = indexOffset, count = mesh.Faces.Count * 3, itemSize = 1, type = "Uint32Array" }, groups };
                details = new { flags = (int)model.Shader, flagNames = model.Shader.ToString(), extraUV = model.TextureData.ExtraUV, animations = model.Animation, bones = model.WeightBone };
                totalModels++; totalVertices += mesh.Vertices.Count; totalTriangles += mesh.Faces.Count;
            }
            else if (chunk is BoneChunk bone) details = new { animations = role == "skeleton" ? new List<BoneAnimation>() : bone.Animation,
                sourceAnimations = role == "skeleton" ? bone.Animation.Select(a => new { a.Name, a.Copy, duration = a.TransformKeyData?.Duration.TotalSeconds }).ToArray() : null };
            else if (chunk is BoxChunk box) details = new { size = Vec(box.Size), box.Unk, box.Unk2, box.Unk3, basis = box.Unk4.Select(Vec) };
            else if (chunk is ShapeChunk shape) details = new { lines = shape.Unk.Select(p => new { a = Vec(p.A), b = Vec(p.B) }) };
            else if (chunk is SkyDirect1Chunk sky) details = new { colors = new[] { sky.Color1,sky.Color2,sky.Color3,sky.Color4,sky.Color5,sky.Color6,sky.Color7 } };
            nodes.Add(new { name = chunk.Name, parent = chunk.SubName, type = chunk.ChunkType.ToString(), matrix = Mat(chunk.Matrix), geometry, details });
        }
        var result = new { name = Path.GetFileName(entry.FullName), role, animationStorage = role == "skeleton" ? "source-only" : "inline", source = entry.FullName[5..].ToLowerInvariant(), header = scene.Header.Name, matrix = Mat(scene.Header.Matrix), nodes, sourceBytes = data.Length, consumedBytes = stream.Position };
        return new(result, diffuseTextures.Distinct().ToArray(), sideTextures.Distinct().ToArray(), totalModels, totalVertices, totalTriangles);

        object Attribute(IEnumerable<float> values, int itemSize)
        {
            var start = binary.BaseStream.Position;
            var count = 0;
            foreach (var value in values)
            {
                if (!float.IsFinite(value)) throw new InvalidDataException("Nonfinite geometry value");
                binary.Write(value); count++;
            }
            return new { byteOffset = start, count = count / itemSize, itemSize, type = "Float32Array" };
        }
        float[] Vec(Vector3 v) => [v.X, v.Y, v.Z];
        float[] Mat(Matrix4x4 m) => [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];


    }
}
