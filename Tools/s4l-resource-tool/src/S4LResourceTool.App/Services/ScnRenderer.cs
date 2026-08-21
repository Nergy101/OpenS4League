using System.Numerics;
using S4League.Scn;

namespace S4LResourceTool.App.Services;

/// <summary>An orbit camera (yaw/pitch around a target) plus perspective projection.</summary>
public struct ScnCamera
{
    public float Yaw;
    public float Pitch;
    public float Distance;
    public Vector3 Target;
    public float FovDegrees;

    public ScnCamera(float yaw, float pitch, float distance, Vector3 target, float fovDegrees = 45f)
    {
        Yaw = yaw; Pitch = pitch; Distance = distance; Target = target; FovDegrees = fovDegrees;
    }
}

/// <summary>
/// A minimal software 3D rasterizer: perspective camera, z-buffer, and lambert shading.
/// Renders a list of <see cref="ScnMesh"/> into a BGRA byte buffer (one control per pixel).
/// Deliberately dependency-free so it can be tested headlessly.
/// </summary>
public static class ScnRenderer
{
    // Directional light, expressed in world space (pointing toward the light).
    private static readonly Vector3 LightDir = Vector3.Normalize(new Vector3(-0.35f, 0.8f, -0.5f));
    private const float Ambient = 0.34f;
    private static readonly byte[] Bg = { 24, 26, 32, 255 }; // dark blue-grey

    /// <summary>Renders the scene into <paramref name="bgra"/> (BGRA, 4 bytes/pixel).</summary>
    /// <param name="textures">Texture-name → decoded texture map for UV sampling (may be null).</param>
    /// <param name="textureOverride">If non-null, forces every face to use this texture name instead
    /// of the per-face assignment. Useful to inspect a single texture/variant.</param>
    public static void Render(
        IReadOnlyList<ScnMesh> meshes,
        ScnCamera camera,
        int width,
        int height,
        byte[] bgra,
        IReadOnlyDictionary<string, ScnTexture>? textures = null,
        string? textureOverride = null)
    {
        if (width <= 0 || height <= 0) return;
        if (bgra.Length < width * height * 4) throw new ArgumentException("buffer too small");

        // Resolve the override texture once, if given.
        ScnTexture? overrideTex = null;
        if (!string.IsNullOrEmpty(textureOverride) && textures is not null)
            textures.TryGetValue(textureOverride, out overrideTex);

        // Clear.
        for (int i = 0; i < width * height; i++)
        {
            bgra[i * 4] = Bg[0]; bgra[i * 4 + 1] = Bg[1]; bgra[i * 4 + 2] = Bg[2]; bgra[i * 4 + 3] = 255;
        }

        var depth = new float[width * height];
        Array.Fill(depth, float.MaxValue);

        // Camera basis (right-handed, Y-up).
        float cp = MathF.Cos(camera.Pitch), sp = MathF.Sin(camera.Pitch);
        float cyaw = MathF.Cos(camera.Yaw), syaw = MathF.Sin(camera.Yaw);
        var dir = new Vector3(cp * syaw, sp, cp * cyaw);  // from target to eye
        var eye = camera.Target + dir * camera.Distance;
        var fwd = Vector3.Normalize(camera.Target - eye);
        var right = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitY));
        var up = Vector3.Cross(right, fwd);

        // Projection: f = 1/tan(fov/2), centered origin, y down.
        float focal = (height * 0.5f) / MathF.Tan(camera.FovDegrees * MathF.PI / 360f);
        float cx = width * 0.5f, cy = height * 0.5f;

        // Light transformed into view space so shading happens in camera space.
        var lightView = new Vector3(
            Vector3.Dot(LightDir, right),
            Vector3.Dot(LightDir, up),
            Vector3.Dot(LightDir, fwd));

        for (int mi = 0; mi < meshes.Count; mi++)
        {
            var mesh = meshes[mi];
            if (mesh.Indices.Length == 0) continue;

            // Project all vertices once.
            int vcount = mesh.Vertices.Length;
            var sx = new float[vcount];
            var sy = new float[vcount];
            var sz = new float[vcount];       // camera-space depth along view dir (positive in front)
            var nx = new float[vcount];
            var ny = new float[vcount];
            var nz = new float[vcount];
            var tu = new float[vcount];
            var tv = new float[vcount];

            for (int v = 0; v < vcount; v++)
            {
                Vector3 rel = mesh.Vertices[v] - eye;
                float rx = Vector3.Dot(rel, right);
                float ry = Vector3.Dot(rel, up);
                float rz = Vector3.Dot(rel, fwd);
                sz[v] = rz;
                if (rz <= 0.01f) continue; // behind or on camera
                sx[v] = cx + (rx * focal) / rz;
                sy[v] = cy - (ry * focal) / rz;

                if (mesh.Uv.Length > v * 2 + 1)
                {
                    tu[v] = mesh.Uv[v * 2];
                    tv[v] = mesh.Uv[v * 2 + 1];
                }

                Vector3 n = mesh.Normals.Length > v ? mesh.Normals[v] : Vector3.UnitY;
                nx[v] = Vector3.Dot(n, right);
                ny[v] = Vector3.Dot(n, up);
                nz[v] = Vector3.Dot(n, fwd);
            }

            // Rasterize triangles.
            for (int t = 0; t + 2 < mesh.Indices.Length; t += 3)
            {
                int i0 = mesh.Indices[t], i1 = mesh.Indices[t + 1], i2 = mesh.Indices[t + 2];
                if (i0 < 0 || i1 < 0 || i2 < 0) continue;
                if (i0 >= vcount || i1 >= vcount || i2 >= vcount) continue;
                if (sz[i0] <= 0.01f || sz[i1] <= 0.01f || sz[i2] <= 0.01f) continue;

                // Screen-space bounding box.
                float minX = MathF.Min(sx[i0], MathF.Min(sx[i1], sx[i2]));
                float maxX = MathF.Max(sx[i0], MathF.Max(sx[i1], sx[i2]));
                float minY = MathF.Min(sy[i0], MathF.Min(sy[i1], sy[i2]));
                float maxY = MathF.Max(sy[i0], MathF.Max(sy[i1], sy[i2]));
                int x0 = Math.Clamp((int)MathF.Floor(minX), 0, width - 1);
                int x1 = Math.Clamp((int)MathF.Ceiling(maxX), 0, width - 1);
                int y0 = Math.Clamp((int)MathF.Floor(minY), 0, height - 1);
                int y1 = Math.Clamp((int)MathF.Ceiling(maxY), 0, height - 1);

                // Edge functions (barycentric): w_i is the area weight of vertex i.
                float area = Edge(i1, i2, i0, sx, sy);
                if (MathF.Abs(area) < 1e-9f) continue;
                float invArea = 1f / area;

                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float w0 = Edge(i1, i2, px, py, sx, sy) * invArea;
                        float w1 = Edge(i2, i0, px, py, sx, sy) * invArea;
                        float w2 = Edge(i0, i1, px, py, sx, sy) * invArea;
                        // Accept either winding (all same sign). The projected y is flipped,
                        // so screen-space triangles may be CW or CCW.
                        if (!((w0 >= 0 && w1 >= 0 && w2 >= 0) || (w0 <= 0 && w1 <= 0 && w2 <= 0)))
                            continue;

                        float depthZ = w0 * sz[i0] + w1 * sz[i1] + w2 * sz[i2];
                        int idx = y * width + x;
                        if (depthZ >= depth[idx]) continue;

                        // Interpolated normal in camera space.
                        float nxc = w0 * nx[i0] + w1 * nx[i1] + w2 * nx[i2];
                        float nyc = w0 * ny[i0] + w1 * ny[i1] + w2 * ny[i2];
                        float nzc = w0 * nz[i0] + w1 * nz[i1] + w2 * nz[i2];
                        float nl = MathF.Sqrt(nxc * nxc + nyc * nyc + nzc * nzc);
                        if (nl < 1e-6f) { nxc = 0; nyc = 0; nzc = 1; nl = 1; }
                        float diffuse = MathF.Abs(nxc * lightView.X + nyc * lightView.Y + nzc * lightView.Z) / nl;
                        float shade = Math.Clamp(Ambient + (1f - Ambient) * diffuse, 0f, 1f);

                        // Determine the texture for this face: the override, else the face's own.
                        ScnTexture? tex = overrideTex;
                        if (tex is null && textures is not null)
                        {
                            int face = t / 3;
                            string? faceName = face < mesh.FaceTextures.Length ? mesh.FaceTextures[face] : null;
                            if (!string.IsNullOrEmpty(faceName))
                                textures.TryGetValue(faceName, out tex);
                        }

                        if (tex is not null)
                        {
                            // Perspective-correct-ish interpolation: interpolate UV directly (adequate for preview).
                            float u = w0 * tu[i0] + w1 * tu[i1] + w2 * tu[i2];
                            float v = w0 * tv[i0] + w1 * tv[i1] + w2 * tv[i2];
                            ScnTextureLoader.Sample(tex, u, v,
                                out byte tr, out byte tg, out byte tb, out byte ta);

                            // Flat base tint also applied so untextured-ish areas keep some tone.
                            bgra[idx * 4] = (byte)(tb * shade);
                            bgra[idx * 4 + 1] = (byte)(tg * shade);
                            bgra[idx * 4 + 2] = (byte)(tr * shade);
                            bgra[idx * 4 + 3] = 255;
                        }
                        else
                        {
                            // Base colour: neutral grey, slightly tinted by mesh index for readability.
                            byte baseR = (byte)(150 + (mi * 37) % 60);
                            byte baseG = (byte)(160 + (mi * 29) % 50);
                            byte baseB = (byte)(170 + (mi * 53) % 40);

                            bgra[idx * 4] = (byte)(baseB * shade);
                            bgra[idx * 4 + 1] = (byte)(baseG * shade);
                            bgra[idx * 4 + 2] = (byte)(baseR * shade);
                            bgra[idx * 4 + 3] = 255;
                        }
                        depth[idx] = depthZ;
                    }
                }
            }
        }
    }

    /// <summary>Computes a reasonable initial camera distance/orbit so the whole model fits on screen.</summary>
    public static ScnCamera FitCamera(IReadOnlyList<ScnMesh> meshes, int width, int height, float yaw = -0.6f, float pitch = 0.4f)
    {
        if (meshes.Count == 0)
            return new ScnCamera(yaw, pitch, 10f, Vector3.Zero);

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var m in meshes)
        {
            foreach (var v in m.Vertices)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }
        }
        var center = (min + max) * 0.5f;
        var radius = Vector3.Distance(min, max) * 0.5f;
        if (radius < 1e-3f) radius = 1f;
        float distance = radius * 2.6f / MathF.Tan(45f * MathF.PI / 360f);
        return new ScnCamera(yaw, pitch, distance, center);
    }

    // Signed edge function: 2x the (signed) area of triangle (a,b,c).
    private static float Edge(int a, int b, int c, float[] sx, float[] sy)
        => (sx[b] - sx[a]) * (sy[c] - sy[a]) - (sy[b] - sy[a]) * (sx[c] - sx[a]);

    // Signed edge function for an arbitrary point p: 2x the (signed) area of (a,b,p).
    private static float Edge(int a, int b, float px, float py, float[] sx, float[] sy)
        => (sx[b] - sx[a]) * (py - sy[a]) - (sy[b] - sy[a]) * (px - sx[a]);
}
