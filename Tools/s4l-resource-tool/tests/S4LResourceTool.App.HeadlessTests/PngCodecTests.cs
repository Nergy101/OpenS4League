using S4LResourceTool.App.Services;
using Xunit;

namespace S4LResourceTool.App.HeadlessTests;

public class PngCodecTests
{
    [Fact]
    public void Decodes_reference_1x1_rgba_png()
    {
        // Python-generated 1x1 opaque red RGBA PNG (independent of the C# encoder).
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP4z8DwHwAFAAH/VscvDQAAAABJRU5ErkJggg==");
        var d = PngCodec.Decode(png);
        Assert.NotNull(d);
        Assert.Equal(1, d.Value.Width);
        Assert.Equal(1, d.Value.Height);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, d.Value.Bgra); // BGRA = red opaque
    }

    [Fact]
    public void Decodes_reference_2x2_rgba_png()
    {
        // Python-generated 2x2 RGBA: red, green, blue, semi-transparent cyan.
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAF0lEQVR42mP4z8DwHwgbGID0CYb//x0APwQHg1ljQ+oAAAAASUVORK5CYII=");
        var d = PngCodec.Decode(png);
        Assert.NotNull(d);
        Assert.Equal(2, d.Value.Width);
        Assert.Equal(2, d.Value.Height);
        var b = d.Value.Bgra;
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, b[0..4]);   // tl red
        Assert.Equal(new byte[] { 0, 255, 0, 128 }, b[4..8]);   // tr green (alpha 128)
        Assert.Equal(new byte[] { 255, 0, 0, 200 }, b[8..12]);  // bl blue (alpha 200)
        Assert.Equal(new byte[] { 255, 255, 0, 64 }, b[12..16]); // br cyan (alpha 64)
    }

    [Fact]
    public void EncodeBgra_round_trips()
    {
        var w = 3;
        var h = 2;
        var src = new byte[w * h * 4];
        for (int i = 0; i < src.Length; i++) src[i] = (byte)((i * 37) % 256); // deterministic pseudo-random
        var png = PngCodec.EncodeBgra(src, w, h);
        var d = PngCodec.Decode(png);
        Assert.NotNull(d);
        Assert.Equal(w, d.Value.Width);
        Assert.Equal(h, d.Value.Height);
        Assert.Equal(src, d.Value.Bgra);
    }

    [Fact]
    public void EncodeGray_round_trips()
    {
        var w = 4;
        var h = 1;
        var alpha = new byte[] { 0, 64, 128, 255 };
        var png = PngCodec.EncodeGray(alpha, w, h);
        var d = PngCodec.Decode(png);
        Assert.NotNull(d);
        Assert.Equal(w, d.Value.Width);
        Assert.Equal(h, d.Value.Height);
        // Grayscale expands to R=G=B=value, alpha=255.
        for (int x = 0; x < w; x++)
        {
            var o = x * 4;
            Assert.Equal(alpha[x], d.Value.Bgra[o]);
            Assert.Equal(alpha[x], d.Value.Bgra[o + 1]);
            Assert.Equal(alpha[x], d.Value.Bgra[o + 2]);
            Assert.Equal(255, d.Value.Bgra[o + 3]);
        }
    }

    [Fact]
    public void Decode_rejects_garbage()
    {
        Assert.Null(PngCodec.Decode(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
        Assert.Null(PngCodec.Decode(Array.Empty<byte>()));
    }
}
