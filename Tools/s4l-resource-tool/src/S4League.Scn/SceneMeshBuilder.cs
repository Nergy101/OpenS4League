using System.Numerics;

namespace S4League.Scn;

/// <summary>A world-space triangle mesh extracted from a <see cref="ModelChunk"/> for rendering.</summary>
public sealed class ScnMesh
{
    public required string Name { get; init; }
    public required Vector3[] Vertices { get; init; }
    public required int[] Indices { get; init; }
    public required Vector3[] Normals { get; init; }   // per-vertex (local->world transformed)
    public required float[] Uv { get; init; }          // per-vertex u,v pairs (0..1)
    public required bool Skinned { get; init; }

    /// <summary>Per-face (triangle) main-texture file name; empty string if the face has none.</summary>
    public string[] FaceTextures { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Turns a parsed <see cref="SceneContainer"/> into world-space renderable meshes.
/// Reimplements the parent/child hierarchy the Unity tool builds from Name/SubName
/// (a chunk's SubName names its parent; empty or header-name SubName means root).
/// Each chunk's Matrix is its local transform relative to its parent.
/// </summary>
public static class SceneMeshBuilder
{
    public static List<ScnMesh> Build(SceneContainer container)
    {
        var meshes = new List<ScnMesh>();

        // Parent lookup: chunk.Name -> chunk. A child's SubName points at its parent's Name.
        var byName = new Dictionary<string, SceneChunk>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in container)
            if (!string.IsNullOrEmpty(c.Name) && !byName.ContainsKey(c.Name))
                byName[c.Name] = c;

        // Memoized world matrix per chunk.
        var world = new Dictionary<SceneChunk, Matrix4x4>();
        Matrix4x4 WorldOf(SceneChunk c)
        {
            if (world.TryGetValue(c, out var m)) return m;
            var local = c.Matrix;
            SceneChunk? parent = null;
            if (!string.IsNullOrEmpty(c.SubName)
                && !string.Equals(c.SubName, container.Header.Name, StringComparison.OrdinalIgnoreCase)
                && byName.TryGetValue(c.SubName, out var p))
            {
                parent = p;
            }
            m = parent is null ? local : local * WorldOf(parent);
            world[c] = m;
            return m;
        }

        foreach (var model in container.Models)
        {
            var m = WorldOf(model);
            var mesh = model.Mesh;
            if (mesh.Vertices.Count == 0) continue;

            var verts = new Vector3[mesh.Vertices.Count];
            var norms = new Vector3[mesh.Normals.Count];
            for (int i = 0; i < verts.Length; i++)
                verts[i] = Vector3.Transform(mesh.Vertices[i], m);
            for (int i = 0; i < norms.Length; i++)
                norms[i] = Vector3.Normalize(Vector3.TransformNormal(mesh.Normals[i], m));

            // Normals may be missing/empty; fall back to a per-face computed normal.
            if (norms.Length == 0)
                norms = ComputeFaceNormals(verts, mesh.Faces);

            var uv = new float[mesh.UV.Count * 2];
            for (int i = 0; i < mesh.UV.Count; i++)
            {
                uv[i * 2] = mesh.UV[i].X;
                uv[i * 2 + 1] = mesh.UV[i].Y;
            }

            // Assign a main-texture name to each face via the TextureData face ranges.
            var faceTextures = new string[mesh.Faces.Count];
            if (model.TextureData is not null && model.TextureData.Textures.Count > 0)
            {
                for (int t = 0; t < model.TextureData.Textures.Count; t++)
                {
                    var entry = model.TextureData.Textures[t];
                    var name = string.IsNullOrEmpty(entry.main_texture) ? "" : entry.main_texture;
                    int start = Math.Max(0, entry.face_offset);
                    int end = Math.Min(mesh.Faces.Count, start + entry.face_count);
                    for (int f = start; f < end; f++)
                        faceTextures[f] = name;
                }
            }

            meshes.Add(new ScnMesh
            {
                Name = model.Name,
                Vertices = verts,
                Indices = mesh.Triangles(),
                Normals = norms,
                Uv = uv,
                Skinned = model.WeightBone.Count > 0,
                FaceTextures = faceTextures
            });
        }

        return meshes;
    }

    /// <summary>Distinct main-texture file names referenced across all meshes, in first-use order.</summary>
    public static IReadOnlyList<string> DistinctTextureNames(IEnumerable<ScnMesh> meshes)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        foreach (var m in meshes)
            foreach (var n in m.FaceTextures)
            {
                if (string.IsNullOrEmpty(n)) continue;
                if (seen.Add(n)) names.Add(n);
            }
        return names;
    }

    /// <summary>Computes a face-normal per vertex (using the first face that references it).</summary>
    static Vector3[] ComputeFaceNormals(Vector3[] verts, IList<Vector3Int> faces)
    {
        var normals = new Vector3[verts.Length];
        foreach (var f in faces)
        {
            if (f.X < 0 || f.Y < 0 || f.Z < 0 || f.X >= verts.Length || f.Y >= verts.Length || f.Z >= verts.Length)
                continue;
            var a = verts[f.X]; var b = verts[f.Y]; var c = verts[f.Z];
            var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            normals[f.X] = n; normals[f.Y] = n; normals[f.Z] = n;
        }
        return normals;
    }
}
