using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Pfim;

namespace S4LClientConfigurator.Services;

/// <summary>Decodes resource images (DDS/TGA via Pfim, everything else via Avalonia) into a Bitmap.</summary>
public static class ImageLoader
{
    private static readonly string[] PfimExtensions = { ".dds", ".tga" };

    public static Bitmap? TryLoad(byte[] data, string extension)
    {
        extension = extension.ToLowerInvariant();
        try
        {
            if (Array.IndexOf(PfimExtensions, extension) >= 0)
            {
                using var image = Pfimage.FromStream(new MemoryStream(data));
                return FromPfim(image);
            }

            using var ms = new MemoryStream(data);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? FromPfim(IImage image)
    {
        var w = image.Width;
        var h = image.Height;
        if (w <= 0 || h <= 0)
            return null;

        var wb = new WriteableBitmap(
            new PixelSize(w, h),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using var fb = wb.Lock();
        var src = image.Data;
        var srcStride = image.Stride;
        var destRow = new byte[w * 4];

        for (var y = 0; y < h; y++)
        {
            var srcOffset = y * srcStride;
            switch (image.Format)
            {
                case Pfim.ImageFormat.Rgba32:
                    Array.Copy(src, srcOffset, destRow, 0, w * 4);
                    break;

                case Pfim.ImageFormat.Rgb24:
                    for (var x = 0; x < w; x++)
                    {
                        var s = srcOffset + x * 3;
                        var d = x * 4;
                        destRow[d] = src[s];         // B
                        destRow[d + 1] = src[s + 1]; // G
                        destRow[d + 2] = src[s + 2]; // R
                        destRow[d + 3] = 255;        // A
                    }
                    break;

                case Pfim.ImageFormat.Rgb8:
                    for (var x = 0; x < w; x++)
                    {
                        var v = src[srcOffset + x];
                        var d = x * 4;
                        destRow[d] = destRow[d + 1] = destRow[d + 2] = v;
                        destRow[d + 3] = 255;
                    }
                    break;

                default:
                    return null; // unsupported pixel format
            }

            Marshal.Copy(destRow, 0, fb.Address + y * fb.RowBytes, w * 4);
        }

        return wb;
    }
}
