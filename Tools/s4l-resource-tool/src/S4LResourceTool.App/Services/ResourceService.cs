using S4League.Resource;

namespace S4LResourceTool.App.Services;

/// <summary>Stateful wrapper around an open <see cref="S4Zip"/> archive.</summary>
public sealed class ResourceService
{
    private readonly List<string> _pendingDeletes = new();

    public S4Zip? Zip { get; private set; }
    public string? ClientPath { get; private set; }
    public bool IsOpen => Zip is not null;

    /// <summary>
    /// Opens an archive. <paramref name="pathOrDirectory"/> may be the client directory, a
    /// subdirectory of it, or the path to a <c>resource.s4hd</c> file directly.
    /// </summary>
    public void Open(string pathOrDirectory)
    {
        var zipPath = ResolveArchivePath(pathOrDirectory)
            ?? throw new FileNotFoundException(
                $"Could not find 'resource.s4hd' in:\n{pathOrDirectory}\n\n" +
                "Select the S4 League folder that contains resource.s4hd (or the file itself).");

        Zip = S4Zip.OpenZip(zipPath);
        ClientPath = Path.GetDirectoryName(zipPath);
        _pendingDeletes.Clear();
    }

    private static string? ResolveArchivePath(string input)
    {
        // A file was selected directly.
        if (File.Exists(input))
            return input;

        if (!Directory.Exists(input))
            return null;

        // Directly inside the chosen folder.
        var direct = Path.Combine(input, "resource.s4hd");
        if (File.Exists(direct))
            return direct;

        // Search a few levels down (case-insensitive), skipping the payload folder.
        try
        {
            return Directory.EnumerateFiles(input, "resource.s4hd", SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public static string FolderOf(string fullName)
    {
        var i = fullName.LastIndexOf('/');
        return i < 0 ? "" : fullName[..i];
    }

    /// <summary>All distinct folder paths (including every ancestor prefix), sorted.</summary>
    public IReadOnlyList<string> AllFolderPaths()
    {
        if (Zip is null) return Array.Empty<string>();

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Zip.Values)
        {
            var folder = FolderOf(entry.FullName);
            while (folder.Length > 0)
            {
                if (!set.Add(folder)) break;
                folder = FolderOf(folder);
            }
        }
        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Entries that live directly inside <paramref name="folder"/> (not in subfolders).</summary>
    public IEnumerable<S4ZipEntry> FilesIn(string folder)
    {
        if (Zip is null) return Enumerable.Empty<S4ZipEntry>();
        return Zip.Values
            .Where(e => string.Equals(FolderOf(e.FullName), folder, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<S4ZipEntry> Search(string term)
    {
        if (Zip is null || string.IsNullOrWhiteSpace(term)) return Enumerable.Empty<S4ZipEntry>();
        return Zip.Values
            .Where(e => e.FullName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase);
    }

    public void Replace(S4ZipEntry entry, byte[] data) => entry.SetData(data);

    /// <summary>Adds a new entry or replaces an existing one; returns the affected entry.</summary>
    public S4ZipEntry AddOrReplace(string fullName, byte[] data)
    {
        fullName = fullName.Replace('\\', '/').ToLowerInvariant();
        var existing = Zip!.Values.FirstOrDefault(e =>
            string.Equals(e.FullName, fullName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.SetData(data);
            return existing;
        }
        return Zip.CreateEntry(fullName, data);
    }

    /// <summary>Marks an entry for deletion (physical file removed on <see cref="Save"/>).</summary>
    public void Delete(S4ZipEntry entry)
    {
        _pendingDeletes.Add(entry.FileName);
        entry.Remove(false);
    }

    public void Save()
    {
        Zip!.Save();
        foreach (var path in _pendingDeletes)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best effort */ }
        }
        _pendingDeletes.Clear();
    }

    public sealed record UnusedResult(IReadOnlyList<string> Files, long TotalBytes);

    /// <summary>Finds payload files on disk that are no longer referenced by any entry.</summary>
    public UnusedResult FindUnused()
    {
        if (Zip is null || !Directory.Exists(Zip.ResourcePath))
            return new UnusedResult(Array.Empty<string>(), 0);

        var used = new HashSet<string>(
            Zip.Values.Select(e => e.Checksum.ToString("x")),
            StringComparer.OrdinalIgnoreCase);

        var unused = Directory.GetFiles(Zip.ResourcePath)
            .Where(p => !used.Contains(Path.GetFileName(p)))
            .ToList();

        long total = 0;
        foreach (var f in unused)
        {
            try { total += new FileInfo(f).Length; } catch { /* ignore */ }
        }
        return new UnusedResult(unused, total);
    }

    public static string HumanSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < units.Length - 1) { order++; len /= 1024; }
        return $"{len:0.##} {units[order]}";
    }
}
