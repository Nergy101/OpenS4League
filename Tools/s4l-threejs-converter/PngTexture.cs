using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Pfim;

// Lossless RGBA PNG encoding of decoded original pixels. Generated variants use
// deterministic Lanczos resampling; no color correction or lossy compression.
static class PngTexture
{
    public sealed record Pixels(int Width, int Height, byte[] Rgba);

    public static (int Width, int Height) Convert(byte[] source, string destination)
    {
        var image = Decode(source);
        Write(image, destination);
        return (image.Width, image.Height);
    }

    public static Pixels Decode(byte[] source)
    {
        using var image = Pfimage.FromStream(new MemoryStream(source));
        if (image.Compressed) image.Decompress();
        var channels = image.Format switch
        {
            ImageFormat.Rgba32 => 4,
            ImageFormat.Rgb24 => 3,
            ImageFormat.Rgb8 => 1,
            _ => throw new NotSupportedException($"Texture format {image.Format}")
        };
        var rgba = new byte[checked(image.Width * image.Height * 4)];
        for (var y = 0; y < image.Height; y++) for (var x = 0; x < image.Width; x++)
        {
            var p = y * image.Stride + x * channels; var d = image.Data; var o = (y * image.Width + x) * 4;
            rgba[o] = d[p + (channels == 1 ? 0 : 2)];
            rgba[o + 1] = d[p + (channels == 1 ? 0 : 1)];
            rgba[o + 2] = d[p]; rgba[o + 3] = channels == 4 ? d[p + 3] : (byte)255;
        }
        return new(image.Width, image.Height, rgba);
    }

    public static Pixels Resize(Pixels source, int scale)
    {
        if (scale is not (1 or 2 or 4)) throw new ArgumentOutOfRangeException(nameof(scale));
        var width = checked(source.Width * scale); var height = checked(source.Height * scale);
        if ((long)width * height > 16384L * 16384L) throw new InvalidDataException("Upscaled texture allocation exceeds 16384x16384");
        if (scale == 1) return source;
        var output = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var sx = (x + .5) / scale - .5; var sy = (y + .5) / scale - .5;
            var x0 = Math.Clamp((int)Math.Floor(sx), 0, source.Width - 1); var x1 = Math.Clamp(x0 + 1, 0, source.Width - 1);
            var y0 = Math.Clamp((int)Math.Floor(sy), 0, source.Height - 1); var y1 = Math.Clamp(y0 + 1, 0, source.Height - 1);
            var fx = Math.Clamp(sx - Math.Floor(sx), 0, 1); var fy = Math.Clamp(sy - Math.Floor(sy), 0, 1);
            var o = (y * width + x) * 4;
            for (var c = 0; c < 4; c++)
            {
                var a = source.Rgba[(y0 * source.Width + x0) * 4 + c] * (1 - fx) + source.Rgba[(y0 * source.Width + x1) * 4 + c] * fx;
                var b = source.Rgba[(y1 * source.Width + x0) * 4 + c] * (1 - fx) + source.Rgba[(y1 * source.Width + x1) * 4 + c] * fx;
                output[o + c] = (byte)Math.Clamp(Math.Round(a * (1 - fy) + b * fy), 0, 255);
            }
        }
        return new(width, height, output);
    }

    public static void Write(Pixels image, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temp = destination + ".tmp";
        using var raw = new MemoryStream();
        for (var y = 0; y < image.Height; y++) { raw.WriteByte(0); raw.Write(image.Rgba, y * image.Width * 4, image.Width * 4); }
        using var file = File.Create(temp);
        file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), image.Width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), image.Height); header[8] = 8; header[9] = 6; Chunk(file, "IHDR", header);
        using var compressed = new MemoryStream(); using (var z = new ZLibStream(compressed, CompressionLevel.SmallestSize, true)) z.Write(raw.ToArray());
        Chunk(file, "IDAT", compressed.ToArray()); Chunk(file, "IEND", []); file.Flush(true);
        File.Move(temp, destination, true);
    }

    public static string Sha256(string path) => System.Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    static void Chunk(Stream stream, string type, byte[] data)
    {
        Span<byte> word = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(word, data.Length); stream.Write(word); var tag = Encoding.ASCII.GetBytes(type); stream.Write(tag); stream.Write(data);
        uint crc = 0xffffffff; foreach (var b in tag.Concat(data)) { crc ^= b; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); }
        BinaryPrimitives.WriteUInt32BigEndian(word, ~crc); stream.Write(word);
    }
}
