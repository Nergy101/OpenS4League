using System.IO.Compression;

namespace S4LResourceTool.App.Services;

/// <summary>
/// Minimal PNG reader/writer (8-bit, non-interlaced, colour types 0/2/4/6) used to hand PNG
/// files to and from the external Real-ESRGAN binary. Self-contained (no Avalonia dependency)
/// so the AI-upscale pipeline is unit-testable headlessly. Encoding writes whole rows with the
/// "None" filter for simplicity; decoding performs full unfiltering (None/Sub/Up/Average/Paeth).
/// </summary>
public static class PngCodec
{
    private static readonly uint[] CrcTable = BuildCrcTable();
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>Encodes a BGRA buffer as an RGBA (colour type 6) PNG.</summary>
    public static byte[] EncodeBgra(ReadOnlySpan<byte> bgra, int w, int h)
    {
        int stride = w * 4;
        var raw = new byte[h * (stride + 1)];
        for (int y = 0; y < h; y++)
        {
            int row = y * (stride + 1);
            raw[row] = 0; // None filter
            int src = y * stride;
            for (int x = 0; x < w; x++)
            {
                int s = src + x * 4;
                int d = row + 1 + x * 4;
                raw[d] = bgra[s + 2];      // R
                raw[d + 1] = bgra[s + 1];  // G
                raw[d + 2] = bgra[s];      // B
                raw[d + 3] = bgra[s + 3];  // A
            }
        }
        return Build(new byte[] { 8, 6, 0, 0, 0 }, w, h, raw);
    }

    /// <summary>Encodes a one-byte-per-pixel alpha buffer as an 8-bit grayscale (colour type 0) PNG.</summary>
    public static byte[] EncodeGray(ReadOnlySpan<byte> alpha, int w, int h)
    {
        var raw = new byte[h * (w + 1)];
        for (int y = 0; y < h; y++)
        {
            int row = y * (w + 1);
            raw[row] = 0; // None filter
            alpha.Slice(y * w, w).CopyTo(raw.AsSpan(row + 1, w));
        }
        return Build(new byte[] { 8, 0, 0, 0, 0 }, w, h, raw);
    }

    /// <summary>
    /// Decodes a PNG into BGRA (colour types 0 grayscale, 2 RGB, 4 gray+alpha, 6 RGBA; 8-bit;
    /// non-interlaced). Returns null for anything unsupported.
    /// </summary>
    public static (int Width, int Height, byte[] Bgra)? Decode(ReadOnlySpan<byte> png)
    {
        if (png.Length < 8 || !png[..8].SequenceEqual(Signature))
            return null;

        int w = 0, h = 0, bitDepth = 0, colorType = 0;
        var idat = new List<byte>();
        int pos = 8;
        bool headerSeen = false;

        while (pos + 12 <= png.Length)
        {
            int length = ReadBe32(png, pos);
            var type = png.Slice(pos + 4, 4);
            int dataStart = pos + 8;

            if (type.SequenceEqual("IHDR"u8))
            {
                headerSeen = true;
                w = ReadBe32(png, dataStart);
                h = ReadBe32(png, dataStart + 4);
                bitDepth = png[dataStart + 8];
                colorType = png[dataStart + 9];
                // compression[10]=0, filter[11]=0, interlace[12]=0
                if (png[dataStart + 12] != 0) return null;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                for (int i = 0; i < length; i++) idat.Add(png[dataStart + i]);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                break;
            }

            pos = dataStart + length + 4; // skip data + crc
        }

        if (!headerSeen || w <= 0 || h <= 0) return null;
        if (bitDepth != 8 || colorType is not (0 or 2 or 4 or 6)) return null;

        int channels = colorType switch { 0 => 1, 2 => 3, 4 => 2, _ => 4 };
        int stride = w * channels;

        byte[] raw;
        try
        {
            using var zms = new MemoryStream(idat.ToArray());
            using var z = new ZLibStream(zms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            z.CopyTo(outMs);
            raw = outMs.ToArray();
        }
        catch { return null; }
        if (raw.Length < h * (stride + 1)) return null;

        var bgra = new byte[w * h * 4];
        var prev = new byte[stride];

        for (int y = 0; y < h; y++)
        {
            int row = y * (stride + 1);
            int filter = raw[row];
            int cur = row + 1;

            // Unfilter in place (None/Sub/Up/Average/Paeth), using the previous row buffer.
            for (int x = 0; x < stride; x++)
            {
                byte a = x >= channels ? raw[cur + x - channels] : (byte)0;
                byte b = y > 0 ? prev[x] : (byte)0;
                byte c = (x >= channels && y > 0) ? prev[x - channels] : (byte)0;
                raw[cur + x] = filter switch
                {
                    0 => raw[cur + x],
                    1 => (byte)(raw[cur + x] + a),
                    2 => (byte)(raw[cur + x] + b),
                    3 => (byte)(raw[cur + x] + (a + b) / 2),
                    4 => (byte)(raw[cur + x] + Paeth(a, b, c)),
                    _ => raw[cur + x],
                };
            }
            raw.AsSpan(cur, stride).CopyTo(prev);

            // Expand to BGRA.
            int rowBgra = y * w * 4;
            for (int x = 0; x < w; x++)
            {
                int s = cur + x * channels;
                int d = rowBgra + x * 4;
                switch (colorType)
                {
                    case 6: // RGBA
                        bgra[d + 2] = raw[s]; bgra[d + 1] = raw[s + 1]; bgra[d] = raw[s + 2]; bgra[d + 3] = raw[s + 3];
                        break;
                    case 2: // RGB
                        bgra[d + 2] = raw[s]; bgra[d + 1] = raw[s + 1]; bgra[d] = raw[s + 2]; bgra[d + 3] = 255;
                        break;
                    case 4: // gray + alpha
                        bgra[d] = bgra[d + 1] = bgra[d + 2] = raw[s]; bgra[d + 3] = raw[s + 1];
                        break;
                    default: // grayscale
                        bgra[d] = bgra[d + 1] = bgra[d + 2] = raw[s]; bgra[d + 3] = 255;
                        break;
                }
            }
        }

        return (w, h, bgra);
    }

    private static byte[] Build(byte[] ihdr, int w, int h, byte[] raw)
    {
        byte[] idat;
        using (var zms = new MemoryStream())
        {
            using (var z = new ZLibStream(zms, CompressionLevel.Optimal))
                z.Write(raw, 0, raw.Length);
            idat = zms.ToArray();
        }

        using var ms = new MemoryStream();
        ms.Write(Signature, 0, Signature.Length);

        var ihdrData = new byte[13];
        WriteBe32(ihdrData, 0, w);
        WriteBe32(ihdrData, 4, h);
        ihdrData[8] = ihdr[0]; // bit depth
        ihdrData[9] = ihdr[1]; // colour type
        ihdrData[10] = ihdr[2]; // compression
        ihdrData[11] = ihdr[3]; // filter
        ihdrData[12] = ihdr[4]; // interlace
        WriteChunk(ms, "IHDR"u8, ihdrData);
        WriteChunk(ms, "IDAT"u8, idat);
        WriteChunk(ms, "IEND"u8, Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void WriteChunk(MemoryStream ms, ReadOnlySpan<byte> type, byte[] data)
    {
        WriteBe32(ms, (uint)data.Length);
        ms.Write(type);
        ms.Write(data, 0, data.Length);
        var crc = Crc32(Combine(type, data));
        WriteBe32(ms, crc);
    }

    private static byte[] Combine(ReadOnlySpan<byte> a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        a.CopyTo(r);
        b.CopyTo(r.AsSpan(a.Length));
        return r;
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (var b in data)
            crc = (crc >> 8) ^ CrcTable[(crc ^ b) & 0xFF];
        return ~crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }

    private static int ReadBe32(ReadOnlySpan<byte> s, int off) =>
        (s[off] << 24) | (s[off + 1] << 16) | (s[off + 2] << 8) | s[off + 3];

    private static void WriteBe32(byte[] dst, int off, int v)
    {
        dst[off] = (byte)(v >> 24); dst[off + 1] = (byte)(v >> 16); dst[off + 2] = (byte)(v >> 8); dst[off + 3] = (byte)v;
    }

    private static void WriteBe32(MemoryStream ms, uint v)
    {
        ms.WriteByte((byte)(v >> 24)); ms.WriteByte((byte)(v >> 16)); ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)v);
    }

    private static byte Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? (byte)a : pb <= pc ? (byte)b : (byte)c;
    }
}
