using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// Archive resolution and byte-preserving dependency storage shared by every mode.
sealed class ConversionAssets(Dictionary<string, ZipArchiveEntry> entries, string output)
{
    public Dictionary<string, object> dependencies { get; } = [];
    public Dictionary<string, string> aliases { get; } = [];
    public SortedSet<string> missing { get; } = [];
    public HashSet<string> texturePaths { get; } = [];
    public byte[] Read(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var data = new MemoryStream();
        stream.CopyTo(data);
        return data.ToArray();
    }
    public void Copy(ZipArchiveEntry entry)
    {
        var path = entry.FullName[5..].ToLowerInvariant();
        if (dependencies.ContainsKey(path)) return;
        var data = Read(entry);
        var target = Path.GetFullPath(Path.Combine(output, "source", path));
        if (!target.StartsWith(output + Path.DirectorySeparatorChar)) throw new InvalidDataException("Unsafe archive path");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(target, data);
        dependencies[path] = new { bytes = data.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(data)), file = "source/" + path };
    }
    public ZipArchiveEntry? Resolve(string reference, string context)
    {
        var key = reference.Replace('\\', '/').ToLowerInvariant();
        var name = Path.GetFileName(key);
        foreach (var candidate in new[] { key, context + key, context + name })
            if (entries.TryGetValue(candidate, out var exact)) return exact;
        var matches = entries.Values.Where(e => Path.GetFileName(e.FullName).Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length == 1) return matches[0];
        if (matches.Length > 1) throw new InvalidDataException($"Ambiguous reference {reference}: {string.Join(", ", matches.Select(e => e.FullName))}");
        // This client stores DDS replacements for legacy .tga references.
        if (key.EndsWith(".tga"))
        {
            var replacement = Resolve(Path.ChangeExtension(key, ".dds"), context);
            if (replacement is not null) aliases[key] = replacement.FullName[5..].ToLowerInvariant();
            return replacement;
        }
        return null;
    }
    public string? Preserve(string reference, string context)
    {
        var entry = Resolve(reference, context);
        if (entry is null) { missing.Add(reference); return null; }
        var key = entry.FullName[5..].ToLowerInvariant();
        var fresh = !dependencies.ContainsKey(key);
        Copy(entry);
        if (Regex.IsMatch(key, @"\.(dds|tga|bmp|png)$")) texturePaths.Add(key);
        if (fresh && (key.EndsWith(".seq") || key.EndsWith(".ini")))
        {
            var text = key.EndsWith(".ini")
                ? string.Join('\n', Encoding.GetEncoding(949).GetString(Read(entry)).Split('\n').Where(l => !l.TrimStart().StartsWith(';')))
                : Encoding.Latin1.GetString(Read(entry));
            foreach (Match m in Regex.Matches(text, @"[a-zA-Z0-9_\-]+\.(?:dds|tga|bmp|png|scn|seq|ogg|wav|ini)\b", RegexOptions.IgnoreCase))
                Preserve(m.Value, Path.GetDirectoryName(key)!.Replace('\\', '/') + "/");
        }
        return key;
    }
}
