using System.Text;

namespace S4League.Resource;

/// <summary>
/// Minimal re-implementation of the BlubLib binary helpers used by the S4Zip format.
/// </summary>
internal static class BinaryExtensions
{
    public static BinaryReader ToBinaryReader(this byte[] data)
        => new(new MemoryStream(data, writable: false), Encoding.UTF8);

    public static byte[] ReadToEnd(this BinaryReader reader)
    {
        using var ms = new MemoryStream();
        reader.BaseStream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Reads a fixed-length field and trims trailing NUL padding.</summary>
    public static string ReadCString(this BinaryReader reader, int length)
    {
        if (length < 1)
            throw new ArgumentOutOfRangeException(nameof(length));
        return Encoding.UTF8.GetString(reader.ReadBytes(length)).TrimEnd('\0');
    }

    /// <summary>Writes a NUL-terminated string zero-padded to exactly <paramref name="maxLength"/> bytes.</summary>
    public static void WriteCString(this BinaryWriter writer, string value, int maxLength)
    {
        var buffer = Encoding.UTF8.GetBytes((value ?? "") + "\0");
        if (buffer.Length > maxLength)
            throw new ArgumentOutOfRangeException(nameof(value), $"value is longer than {maxLength}");
        writer.Write(buffer);
        var pad = maxLength - buffer.Length;
        if (pad > 0)
            writer.Write(new byte[pad]);
    }

    public static byte[] ToArray(this BinaryWriter writer)
        => ((MemoryStream)writer.BaseStream).ToArray();

    public static byte[] FastClone(this byte[] data)
        => (byte[])data.Clone();
}
