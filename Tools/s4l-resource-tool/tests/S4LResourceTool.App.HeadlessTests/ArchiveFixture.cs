using System.Text;
using S4League.Resource;

namespace S4LResourceTool.App.HeadlessTests;

/// <summary>Builds a throwaway S4 League client directory (resource.s4hd + _resources) for tests.</summary>
public sealed class ArchiveFixture : IDisposable
{
    public string ClientDir { get; }

    public ArchiveFixture()
    {
        ClientDir = Path.Combine(Path.GetTempPath(), "s4l_app_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ClientDir, "_resources"));

        var zip = S4Zip.Create(Path.Combine(ClientDir, "resource.s4hd"));
        zip.CreateEntry("readme.txt", Encoding.UTF8.GetBytes("hello from the S4 League Resource Tool"));
        zip.CreateEntry("gui/hud/config.ini", Encoding.UTF8.GetBytes("[hud]\nscale=1.0\n"));
        zip.CreateEntry("gui/texture/logo.dds", MakeUncompressedDds(4, 4));
        zip.CreateEntry("gui/texture/big.dds", MakeUncompressedDds(128, 128));
        zip.CreateEntry("sound/effects/blip.bin", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        zip.Save();
    }

    /// <summary>Creates a minimal uncompressed 32bpp BGRA DDS that Pfim can decode.</summary>
    private static byte[] MakeUncompressedDds(int width, int height)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write(0x20534444);          // "DDS "
        w.Write(124);                 // dwSize
        w.Write(0x1 | 0x2 | 0x4 | 0x1000); // CAPS | HEIGHT | WIDTH | PIXELFORMAT
        w.Write(height);
        w.Write(width);
        w.Write(0);                   // pitch/linear size
        w.Write(0);                   // depth
        w.Write(0);                   // mipmap count
        for (var i = 0; i < 11; i++) w.Write(0); // reserved

        // DDS_PIXELFORMAT (32 bytes)
        w.Write(32);                  // dwSize
        w.Write(0x1 | 0x40);          // ALPHAPIXELS | RGB
        w.Write(0);                   // fourCC
        w.Write(32);                  // RGB bit count
        w.Write(0x00FF0000u);         // R mask
        w.Write(0x0000FF00u);         // G mask
        w.Write(0x000000FFu);         // B mask
        w.Write(0xFF000000u);         // A mask

        w.Write(0x1000);              // caps: TEXTURE
        w.Write(0); w.Write(0); w.Write(0); // caps2/3/4
        w.Write(0);                   // reserved2

        // Pixel data: BGRA per pixel
        for (var i = 0; i < width * height; i++)
        {
            w.Write((byte)0x10); // B
            w.Write((byte)0x80); // G
            w.Write((byte)0xF0); // R
            w.Write((byte)0xFF); // A
        }

        w.Flush();
        return ms.ToArray();
    }

    public void Dispose()
    {
        try { Directory.Delete(ClientDir, true); } catch { /* best effort */ }
    }
}
