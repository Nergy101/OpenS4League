using System;

namespace S4LResourceTool.App.Services;

/// <summary>An upscaled BGRA image (bottom-up row 0 = top, matching the source).</summary>
public readonly struct UpscaledTexture
{
    public required byte[] Bgra { get; init; } // width*height*4, BGRA
    public required int Width { get; init; }
    public required int Height { get; init; }
}

/// <summary>
/// Pure in-process, alpha-aware texture upscaler (no external tools or GPU needed).
/// Works on a raw BGRA buffer. Quality comes from Lanczos-3 resampling combined with
/// iterative 2x passes and a light unsharp-mask sharpen between passes — the classic
/// "progressive doubling" structure used by standalone upscalers. The alpha channel is
/// resampled through the same filter (so transparent edges stay clean) and is then left
/// untouched by the sharpen step (avoids halos on alpha edges).
/// </summary>
public static class TextureUpscaler
{
    /// <summary>Supported target scale factors (powers of two).</summary>
    public static readonly int[] SupportedFactors = { 2, 4, 8 };

    // Lanczos-3 support radius, measured in source pixels. A fixed source-space support
    // means consecutive output pixels interpolate among the same small neighbourhood,
    // which is the correct behaviour when only ever upscaling (never downscaling).
    private const double Radius = 3.0;

    /// <summary>Upscales <paramref name="bgra"/> by <paramref name="factor"/> (2, 4 or 8).</summary>
    public static UpscaledTexture Upscale(byte[] bgra, int w, int h, int factor)
    {
        if (factor is not (2 or 4 or 8))
            throw new ArgumentOutOfRangeException(nameof(factor), "Only 2x, 4x and 8x are supported.");

        var cur = bgra;
        int cw = w, ch = h;

        int passes = factor switch { 2 => 1, 4 => 2, _ => 3 };
        for (int p = 0; p < passes; p++)
        {
            cur = ResizeLanczos(cur, cw, ch, cw * 2, ch * 2);
            cw *= 2;
            ch *= 2;
            // Sharpen RGB only; alpha was already resampled cleanly and is left alone here.
            cur = UnsharpMask(cur, cw, ch, amount: 0.55);
        }

        return new UpscaledTexture { Bgra = cur, Width = cw, Height = ch };
    }

    /// <summary>Separable Lanczos-3 resize (nearest-sampling interpolation, safe for upscale-only).</summary>
    private static byte[] ResizeLanczos(byte[] src, int inW, int inH, int outW, int outH)
    {
        double scaleX = (double)inW / outW;
        double scaleY = (double)inH / outH;

        // ---- Horizontal pass: (inW x inH) -> (outW x inH) ----
        var temp = new byte[outW * inH * 4];
        for (int y = 0; y < inH; y++)
        {
            int srcRow = y * inW * 4;
            int dstRow = y * outW * 4;
            for (int x = 0; x < outW; x++)
            {
                double center = (x + 0.5) * scaleX - 0.5;
                int i0 = (int)Math.Ceiling(center - Radius);
                int i1 = (int)Math.Floor(center + Radius);
                if (i0 < 0) i0 = 0;
                if (i1 > inW - 1) i1 = inW - 1;

                double b = 0, g = 0, r = 0, a = 0, wsum = 0;
                for (int i = i0; i <= i1; i++)
                {
                    double w = Lanczos3(i - center);
                    if (w == 0) continue;
                    int o = srcRow + i * 4;
                    b += w * src[o];
                    g += w * src[o + 1];
                    r += w * src[o + 2];
                    a += w * src[o + 3];
                    wsum += w;
                }

                if (wsum != 0) { b /= wsum; g /= wsum; r /= wsum; a /= wsum; }
                int d = dstRow + x * 4;
                temp[d] = Clamp255(b);
                temp[d + 1] = Clamp255(g);
                temp[d + 2] = Clamp255(r);
                temp[d + 3] = Clamp255(a);
            }
        }

        // ---- Vertical pass: (outW x inH) -> (outW x outH) ----
        var dst = new byte[outW * outH * 4];
        for (int y = 0; y < outH; y++)
        {
            double center = (y + 0.5) * scaleY - 0.5;
            int j0 = (int)Math.Ceiling(center - Radius);
            int j1 = (int)Math.Floor(center + Radius);
            if (j0 < 0) j0 = 0;
            if (j1 > inH - 1) j1 = inH - 1;

            int dstRow = y * outW * 4;
            for (int x = 0; x < outW; x++)
            {
                double b = 0, g = 0, r = 0, a = 0, wsum = 0;
                for (int j = j0; j <= j1; j++)
                {
                    double w = Lanczos3(j - center);
                    if (w == 0) continue;
                    int o = j * outW * 4 + x * 4;
                    b += w * temp[o];
                    g += w * temp[o + 1];
                    r += w * temp[o + 2];
                    a += w * temp[o + 3];
                    wsum += w;
                }

                if (wsum != 0) { b /= wsum; g /= wsum; r /= wsum; a /= wsum; }
                int d = dstRow + x * 4;
                dst[d] = Clamp255(b);
                dst[d + 1] = Clamp255(g);
                dst[d + 2] = Clamp255(r);
                dst[d + 3] = Clamp255(a);
            }
        }

        return dst;
    }

    private static double Lanczos3(double x)
    {
        if (x < -Radius || x >= Radius) return 0;
        if (x == 0) return 1;
        double p = Math.PI * x;          // pi * x
        double pp = p / Radius;          // pi * x / a
        return (Math.Sin(p) * Math.Sin(pp)) / (p * pp);
    }

    /// <summary>
    /// Unsharp mask on the RGB channels only (alpha is passed through unchanged so
    /// transparent edges don't grow bright halos). <paramref name="amount"/> controls how
    /// strongly edges are emphasised; the blur is a small 3x3 neighbourhood average.
    /// </summary>
    private static byte[] UnsharpMask(byte[] bgra, int w, int h, double amount)
    {
        var dst = new byte[bgra.Length];
        for (int y = 0; y < h; y++)
        {
            int row = y * w * 4;
            for (int x = 0; x < w; x++)
            {
                int o = row + x * 4;
                dst[o + 3] = bgra[o + 3]; // alpha untouched

                for (int c = 0; c < 3; c++)
                {
                    int idx = o + c;
                    double center = bgra[idx];
                    double sum = 0;
                    int n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= h) continue;
                        int rr = yy * w * 4;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= w) continue;
                            sum += bgra[rr + xx * 4 + c];
                            n++;
                        }
                    }
                    dst[idx] = Clamp255(center + amount * (center - sum / n));
                }
            }
        }
        return dst;
    }

    private static byte Clamp255(double v) => v <= 0 ? (byte)0 : (v >= 255 ? (byte)255 : (byte)Math.Round(v));
}
