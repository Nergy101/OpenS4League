using S4League.Resource.Internal;

namespace S4League.Resource;

/// <summary>
/// A single entry inside an <see cref="S4Zip"/> container. The payload is stored on disk in the
/// container's <c>_resources</c> folder as a file named after the entry's hex checksum.
/// </summary>
public sealed class S4ZipEntry
{
    private const int CompressionThreshold = 1048576; // 1024 * 1024 (NeoNetsphere)

    public string Name { get; }
    public string FullName { get; }
    public int Length { get; private set; }
    public long Checksum { get; private set; }
    public int Unk { get; }
    public S4Zip Archive { get; }

    /// <summary>On-disk path of the payload file (hex checksum name inside <c>_resources</c>).</summary>
    public string FileName => Path.Combine(Archive.ResourcePath, Checksum.ToString("x"));

    internal S4ZipEntry(S4Zip archive, string fullName)
    {
        Archive = archive;
        FullName = fullName;
        Name = Path.GetFileName(fullName);
    }

    internal S4ZipEntry(S4Zip archive, string fullName, int length, long checksum, int unk)
        : this(archive, fullName)
    {
        Length = length;
        Checksum = checksum;
        Unk = unk;
    }

    /// <summary>Reads and decodes the payload from disk.</summary>
    public byte[] GetData() => Decrypt(File.ReadAllBytes(FileName));

    /// <summary>Encodes and writes the payload to disk, updating <see cref="Checksum"/>/<see cref="Length"/>.</summary>
    public void SetData(byte[] data)
    {
        var encrypted = Encrypt(data);
        File.WriteAllBytes(FileName, encrypted);
    }

    public void Remove() => Remove(true);

    public void Remove(bool deleteFromDisk) => Archive.RemoveEntry(FullName, deleteFromDisk);

    public override string ToString() => FullName;

    private byte[] Encrypt(byte[] data)
    {
        data = data.FastClone(); // never mutate the caller's buffer
        var isX7 = Name.EndsWith(".x7", StringComparison.InvariantCultureIgnoreCase);
        if (Name.EndsWith(".lua", StringComparison.InvariantCultureIgnoreCase) || isX7)
        {
            if (isX7)
                data = data.EncryptX7();
            data = data.EncryptSeed();
        }

        Checksum = GetChecksum(data);
        Length = data.Length;

        S4Crypt.OldCapped32.Encrypt(data);
        if (data.Length < CompressionThreshold)
            data = MiniLzo.Compress(data);
        data.SwapBytes();

        return data;
    }

    private byte[] Decrypt(byte[] data)
    {
        data.SwapBytes();
        if (data.Length < CompressionThreshold)
            data = MiniLzo.Decompress(data, Length);
        S4Crypt.OldCapped32.Decrypt(data);

        var isX7 = Name.EndsWith(".x7", StringComparison.InvariantCultureIgnoreCase);
        if (Name.EndsWith(".lua", StringComparison.InvariantCultureIgnoreCase) || isX7)
        {
            data = data.DecryptSeed();
            if (isX7)
                data = data.DecryptX7();
        }

        return data;
    }

    private long GetChecksum(byte[] data) => data.S4CRC(FullName);
}
