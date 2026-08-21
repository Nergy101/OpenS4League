using System.Collections;

namespace S4League.Resource;

/// <summary>
/// Reader/writer for S4 League's <c>resource.s4hd</c> container. The container holds an index of
/// entries; the actual (encrypted + compressed) payloads live as individual files, named by their
/// hex checksum, inside the sibling <c>_resources</c> directory.
/// Faithful port of NetspherePirates' S4Zip to modern, cross-platform .NET.
/// </summary>
public sealed class S4Zip : IReadOnlyDictionary<string, S4ZipEntry>
{
    private readonly Dictionary<string, S4ZipEntry> _entries = new();

    public string ZipPath { get; }
    public string ResourcePath { get; }

    private S4Zip(string zipPath)
    {
        ZipPath = zipPath;
        ResourcePath = Path.Combine(Path.GetDirectoryName(zipPath) ?? "", "_resources");
    }

    public static S4Zip OpenZip(string fileName)
    {
        var zip = new S4Zip(fileName);
        zip.Open(fileName);
        return zip;
    }

    /// <summary>Creates a new, empty archive. Call <see cref="Save()"/> to write it to disk.</summary>
    public static S4Zip Create(string zipPath) => new(zipPath);

    public void Open(string fileName) => Open(File.ReadAllBytes(fileName));

    // Some clients (e.g. S4Max 4.5.0.x) omit the container's outer block-transpose and use a
    // non-1 version magic. Detected on open and preserved on save.
    private bool _containerSwap = true;
    private int _headerVersion = 1;

    public void Open(byte[] data)
    {
        if (TryParse(data, swapOuter: true) || TryParse(data, swapOuter: false))
            return;
        throw new InvalidDataException("Invalid or unsupported s4 league file container");
    }

    private bool TryParse(byte[] fileData, bool swapOuter)
    {
        byte[] plain;
        try { plain = fileData.DecryptSeed(swapOuter); }
        catch { return false; }

        if (plain.Length < 8) return false;
        var version = BitConverter.ToInt32(plain, 0);
        var entryCount = BitConverter.ToInt32(plain, 4);
        if (entryCount < 0 || entryCount > 20_000_000) return false;

        // Entry blocks are always 272 bytes (256 name + 8 checksum + 4 length + 4 unk); use that
        // as a structural sanity check to pick the correct decryption variant.
        if (entryCount > 0)
        {
            if (plain.Length < 12 || BitConverter.ToInt32(plain, 8) != 272)
                return false;
        }

        var entries = new Dictionary<string, S4ZipEntry>();
        using var r = plain.ToBinaryReader();
        r.ReadInt32();
        r.ReadInt32();
        try
        {
            for (var i = 0; i < entryCount; i++)
            {
                var entrySize = r.ReadInt32();
                var entryData = r.ReadBytes(entrySize);
                if (entryData.Length != entrySize) return false;
                S4Crypt.OldCapped32.Decrypt(entryData);

                using var er = entryData.ToBinaryReader();
                var fullName = er.ReadCString(256).ToLowerInvariant();
                var checksum = er.ReadInt64();
                var length = er.ReadInt32();
                var unk = er.ReadInt32();
                entries[fullName] = new S4ZipEntry(this, fullName, length, checksum, unk);
            }
        }
        catch (EndOfStreamException) { return false; }

        _entries.Clear();
        foreach (var kv in entries)
            _entries[kv.Key] = kv.Value;
        _containerSwap = swapOuter;
        _headerVersion = version;
        return true;
    }

    public void Save() => Save(ZipPath);

    public void Save(string fileName)
    {
        using var w = new BinaryWriter(new MemoryStream());
        w.Write(_headerVersion);
        w.Write(_entries.Count);
        foreach (var entry in _entries.Values)
        {
            using var entryWriter = new BinaryWriter(new MemoryStream());
            entryWriter.WriteCString(entry.FullName, 256);
            entryWriter.Write(entry.Checksum);
            entryWriter.Write(entry.Length);
            entryWriter.Write(entry.Unk);

            var entryData = entryWriter.ToArray();
            S4Crypt.OldCapped32.Encrypt(entryData);

            w.Write(entryData.Length);
            w.Write(entryData);
        }

        var data = w.ToArray().EncryptSeed(_containerSwap);
        File.WriteAllBytes(fileName, data);
    }

    public S4ZipEntry CreateEntry(string fullName, byte[] data)
    {
        fullName = fullName.ToLowerInvariant();
        if (_entries.ContainsKey(fullName))
            throw new ArgumentException(fullName + " already exists", nameof(fullName));

        var entry = new S4ZipEntry(this, fullName);
        entry.SetData(data);
        _entries.Add(fullName, entry);
        return entry;
    }

    public S4ZipEntry RemoveEntry(string fullName) => RemoveEntry(fullName, false);

    public S4ZipEntry RemoveEntry(string fullName, bool deleteFromDisk)
    {
        fullName = fullName.ToLowerInvariant();
        if (!_entries.TryGetValue(fullName, out var entry))
            throw new ArgumentException(fullName + " does not exist", nameof(fullName));

        if (deleteFromDisk && File.Exists(entry.FileName))
            File.Delete(entry.FileName);
        _entries.Remove(fullName);
        return entry;
    }

    #region IReadOnlyDictionary

    public int Count => _entries.Count;
    public IEnumerable<string> Keys => _entries.Keys;
    public IEnumerable<S4ZipEntry> Values => _entries.Values;

    public S4ZipEntry? this[string key]
    {
        get
        {
            TryGetValue(key.ToLowerInvariant(), out var entry);
            return entry;
        }
    }

    S4ZipEntry IReadOnlyDictionary<string, S4ZipEntry>.this[string key] => _entries[key.ToLowerInvariant()];

    public bool ContainsKey(string key) => _entries.ContainsKey(key.ToLowerInvariant());

    public bool TryGetValue(string key, out S4ZipEntry value) => _entries.TryGetValue(key.ToLowerInvariant(), out value!);

    public IEnumerator<KeyValuePair<string, S4ZipEntry>> GetEnumerator() => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}
