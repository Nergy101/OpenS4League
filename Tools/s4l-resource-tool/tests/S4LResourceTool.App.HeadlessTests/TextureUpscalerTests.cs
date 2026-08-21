using S4LResourceTool.App.Services;
using Xunit;

namespace S4LResourceTool.App.HeadlessTests;

/// <summary>Headless tests for the pure in-process <see cref="TextureUpscaler"/>.</summary>
public class TextureUpscalerTests
{
    [Fact]
    public void Upscale_solid_color_keeps_dimensions_and_exact_pixel()
    {
        // 2x2 solid blue with 50% alpha.
        var src = new byte[2 * 2 * 4];
        for (int i = 0; i < src.Length; i += 4)
        {
            src[i] = 255;      // B
            src[i + 1] = 0;    // G
            src[i + 2] = 0;    // R
            src[i + 3] = 128;  // A
        }

        var r = TextureUpscaler.Upscale(src, 2, 2, 8);

        Assert.Equal(16, r.Width);
        Assert.Equal(16, r.Height);
        Assert.Equal(16 * 16 * 4, r.Bgra.Length);

        // Lanczos of a constant is the same constant; the sharpen step is a no-op on flat areas.
        for (int i = 0; i < r.Bgra.Length; i += 4)
        {
            Assert.Equal(255, r.Bgra[i]);
            Assert.Equal(0, r.Bgra[i + 1]);
            Assert.Equal(0, r.Bgra[i + 2]);
            Assert.Equal(128, r.Bgra[i + 3]);
        }
    }

    [Fact]
    public void Upscale_preserves_alpha_regions()
    {
        // 2x2: left column opaque white, right column fully transparent black.
        var src = new byte[2 * 2 * 4];
        for (int y = 0; y < 2; y++)
        {
            int o = y * 2 * 4;
            // x=0 opaque white
            src[o] = 255; src[o + 1] = 255; src[o + 2] = 255; src[o + 3] = 255;
            // x=1 transparent black
            src[o + 4] = 0; src[o + 5] = 0; src[o + 6] = 0; src[o + 7] = 0;
        }

        var r = TextureUpscaler.Upscale(src, 2, 2, 4);
        Assert.Equal(8, r.Width);
        Assert.Equal(8, r.Height);

        int P(int x, int y) => (y * 8 + x) * 4;

        // Interior of the left (opaque) region stays white and opaque.
        int li = P(1, 4);
        Assert.True(r.Bgra[li + 3] > 200, $"left alpha expected high, got {r.Bgra[li + 3]}");
        Assert.True(r.Bgra[li + 2] > 200, "left should be bright (white)");

        // Interior of the right (transparent) region stays fully transparent.
        int ri = P(6, 4);
        Assert.True(r.Bgra[ri + 3] < 30, $"right alpha expected ~0, got {r.Bgra[ri + 3]}");

        // Alpha channel was not modified by the sharpen pass: it must equal the resampled value.
        // Spot-check that transparent-edge pixels never gained alpha (no halo).
        for (int y = 0; y < 8; y++)
            for (int x = 5; x < 8; x++)
                Assert.True(r.Bgra[P(x, y) + 3] < 120,
                    $"unexpected alpha halo at ({x},{y}): {r.Bgra[P(x, y) + 3]}");
    }

    [Fact]
    public void Upscale_rejects_unsupported_factors()
    {
        var src = new byte[4];
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureUpscaler.Upscale(src, 1, 1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextureUpscaler.Upscale(src, 1, 1, 16));
    }
}
