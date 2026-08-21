using System.Numerics;
using S4League.Scn;
using S4LResourceTool.App.Services;
using Xunit;

namespace S4LResourceTool.App.HeadlessTests;

/// <summary>Headless tests for the .scn parser and software renderer (no GUI needed).</summary>
public class ScnRendererTests
{
    static IReadOnlyList<ScnMesh> MakeQuad()
    {
        var container = new SceneContainer { Header = { Name = "t" } };
        var model = new ModelChunk(container) { Name = "m" };
        model.Mesh.Vertices.Add(new Vector3(-1, -1, 0));
        model.Mesh.Vertices.Add(new Vector3(1, -1, 0));
        model.Mesh.Vertices.Add(new Vector3(1, 1, 0));
        model.Mesh.Vertices.Add(new Vector3(-1, 1, 0));
        model.Mesh.Faces.Add(new Vector3Int(0, 1, 2));
        model.Mesh.Faces.Add(new Vector3Int(0, 2, 3));
        for (int i = 0; i < 4; i++) model.Mesh.Normals.Add(Vector3.UnitZ);
        container.Add(model);
        return SceneMeshBuilder.Build(container);
    }

    [Fact]
    public void Render_writes_some_non_background_pixels()
    {
        var meshes = MakeQuad();
        const int w = 64, h = 64;
        var buf = new byte[w * h * 4];
        var cam = new ScnCamera(0f, 0f, 6f, Vector3.Zero);
        ScnRenderer.Render(meshes, cam, w, h, buf);

        // Count pixels that differ from the background colour.
        int lit = 0;
        for (int i = 0; i < w * h; i++)
        {
            if (buf[i * 4] != 24 || buf[i * 4 + 1] != 26 || buf[i * 4 + 2] != 32)
                lit++;
        }
        Assert.True(lit > 200, $"expected a large lit quad region, got {lit} lit pixels");
    }

    [Fact]
    public void FitCamera_centers_on_bounds()
    {
        var cam = ScnRenderer.FitCamera(MakeQuad(), 100, 100);
        Assert.True(cam.Distance > 0);
        Assert.True(float.IsFinite(cam.Target.X));
    }

    [Fact]
    public void Render_uses_override_texture()
    {
        // A quad with UV covering the whole 2x2 texture; force the "red" override.
        var container = new SceneContainer { Header = { Name = "t" } };
        var model = new ModelChunk(container) { Name = "m" };
        model.Mesh.Vertices.Add(new Vector3(-1, -1, 0));
        model.Mesh.Vertices.Add(new Vector3(1, -1, 0));
        model.Mesh.Vertices.Add(new Vector3(1, 1, 0));
        model.Mesh.Vertices.Add(new Vector3(-1, 1, 0));
        model.Mesh.Faces.Add(new Vector3Int(0, 1, 2));
        model.Mesh.Faces.Add(new Vector3Int(0, 2, 3));
        for (int i = 0; i < 4; i++) model.Mesh.Normals.Add(Vector3.UnitZ);
        for (int i = 0; i < 4; i++) model.Mesh.UV.Add(new Vector2(i % 2 == 0 ? 0 : 1, i < 2 ? 0 : 1));
        container.Add(model);
        var meshes = SceneMeshBuilder.Build(container);

        // 2x2 pure red texture.
        var tex = new ScnTexture { Name = "red", Width = 2, Height = 2, Bgra = new byte[2 * 2 * 4] };
        for (int i = 0; i < 2 * 2 * 4; i += 4) { tex.Bgra[i] = 0; tex.Bgra[i + 1] = 0; tex.Bgra[i + 2] = 255; tex.Bgra[i + 3] = 255; }
        var textures = new Dictionary<string, ScnTexture>(StringComparer.OrdinalIgnoreCase) { ["red"] = tex };

        const int w = 64, h = 64;
        var buf = new byte[w * h * 4];
        var cam = new ScnCamera(0f, 0f, 6f, Vector3.Zero);
        ScnRenderer.Render(meshes, cam, w, h, buf, textures, textureOverride: "red");

        int lit = 0;
        for (int i = 0; i < w * h; i++)
            if (buf[i * 4] != 24 || buf[i * 4 + 1] != 26 || buf[i * 4 + 2] != 32)
                lit++;
        Assert.True(lit > 200, $"override texture not rendered, {lit} lit pixels");

        // Spot-check a pixel: a pure-red texture yields red > green and red > blue,
        // which the neutral-grey flat colour never does.
        int found = -1;
        for (int i = 0; i < w * h; i++)
        {
            if (buf[i * 4 + 2] > buf[i * 4 + 1] && buf[i * 4 + 2] > buf[i * 4]) { found = i; break; }
        }
        Assert.True(found >= 0, "expected a strong-red textured pixel from the override");
    }
}
