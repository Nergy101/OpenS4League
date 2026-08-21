using System.Runtime.InteropServices;
using Pfim;
using S4League.Resource;

namespace S4League.View;

/// <summary>A decoded 2D texture (raw BGRA, row 0 = top) ready for UV sampling.</summary>
public sealed class ScnTexture
{
    public required string Name { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required byte[] Bgra { get; init; } // width*height*4, BGRA
}

/// <summary>Resolves .scn-referenced texture files from a resource archive and decodes them.</summary>
public static class ScnTextureLoader
{
    /// <summary>Finds the archive entry whose file name equals <paramref name="textureName"/>
    /// (case-insensitive, ignoring any directory) and decodes it. Null if not found/decodable.</summary>
    public static ScnTexture? TryLoad(S4Zip? zip, string textureName)
    {
        if (zip is null) return null;
        var baseName = textureName.Replace('\\', '/');
        var slash = baseName.LastIndexOf('/');
        if (slash >= 0) baseName = baseName[(slash + 1)..];

        var entry = zip.Values.FirstOrDefault(e =>
            string.Equals(e.Name, baseName, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;

        byte[] data;
        try { data = entry.GetData(); }
        catch { return null; }

        return Decode(data, baseName);
    }

    /// <summary>Decodes a DDS/TGA/PNG buffer into a raw BGRA <see cref="ScnTexture"/>.</summary>
    public static ScnTexture? Decode(byte[] data, string name)
    {
        try
        {
            using var image = Pfimage.FromStream(new MemoryStream(data));
            int w = image.Width, h = image.Height;
            if (w <= 0 || h <= 0) return null;

            var bgra = new byte[w * h * 4];
            var src = image.Data;
            int srcStride = image.Stride;

            for (int y = 0; y < h; y++)
            {
                int srcOff = y * srcStride;
                int dstOff = y * w * 4;
                switch (image.Format)
                {
                    case ImageFormat.Rgba32:
                        Buffer.BlockCopy(src, srcOff, bgra, dstOff, w * 4);
                        break;
                    case ImageFormat.Rgb24:
                        for (int x = 0; x < w; x++)
                        {
                            int s = srcOff + x * 3, d = dstOff + x * 4;
                            bgra[d] = src[s]; bgra[d + 1] = src[s + 1]; bgra[d + 2] = src[s + 2]; bgra[d + 3] = 255;
                        }
                        break;
                    case ImageFormat.Rgb8:
                        for (int x = 0; x < w; x++)
                        {
                            int d = dstOff + x * 4;
                            bgra[d] = bgra[d + 1] = bgra[d + 2] = src[srcOff + x]; bgra[d + 3] = 255;
                        }
                        break;
                    default:
                        return null;
                }
            }

            // S4 .dds files are stored bottom-up; flip vertically so UV mapping matches TGA.
            if (name.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                FlipVertical(bgra, w, h);

            return new ScnTexture { Name = name, Width = w, Height = h, Bgra = bgra };
        }
        catch
        {
            return null;
        }
    }

    private static void FlipVertical(byte[] bgra, int w, int h)
    {
        var row = new byte[w * 4];
        for (int y = 0; y < h / 2; y++)
        {
            int top = y * w * 4;
            int bottom = (h - 1 - y) * w * 4;
            Buffer.BlockCopy(bgra, top, row, 0, w * 4);
            Buffer.BlockCopy(bgra, bottom, bgra, top, w * 4);
            Buffer.BlockCopy(row, 0, bgra, bottom, w * 4);
        }
    }

    /// <summary>Samples a texture with bilinear filtering at normalized (u,v), clamped.</summary>
    public static void Sample(ScnTexture tex, float u, float v,
        out byte r, out byte g, out byte b, out byte a)
    {
        float fx = u * tex.Width - 0.5f;
        float fy = v * tex.Height - 0.5f;
        int x0 = Clamp((int)MathF.Floor(fx), 0, tex.Width - 1);
        int y0 = Clamp((int)MathF.Floor(fy), 0, tex.Height - 1);
        int x1 = Clamp(x0 + 1, 0, tex.Width - 1);
        int y1 = Clamp(y0 + 1, 0, tex.Height - 1);
        float tx = Math.Clamp(fx - x0, 0, 1);
        float ty = Math.Clamp(fy - y0, 0, 1);

        int i00 = (y0 * tex.Width + x0) * 4;
        int i10 = (y0 * tex.Width + x1) * 4;
        int i01 = (y1 * tex.Width + x0) * 4;
        int i11 = (y1 * tex.Width + x1) * 4;
        var bg = tex.Bgra;

        r = (byte)Lerp(Lerp(bg[i00 + 2], bg[i10 + 2], tx), Lerp(bg[i01 + 2], bg[i11 + 2], tx), ty);
        g = (byte)Lerp(Lerp(bg[i00 + 1], bg[i10 + 1], tx), Lerp(bg[i01 + 1], bg[i11 + 1], tx), ty);
        b = (byte)Lerp(Lerp(bg[i00], bg[i10], tx), Lerp(bg[i01], bg[i11], tx), ty);
        a = (byte)Lerp(Lerp(bg[i00 + 3], bg[i10 + 3], tx), Lerp(bg[i01 + 3], bg[i11 + 3], tx), ty);
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
