using S4League.Resource;

namespace S4LClientConfigurator.Services;

/// <summary>Stateful wrapper around an open <see cref="S4Zip"/> archive.</summary>
public sealed class ResourceService
{
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

        // Search a few levels down (case-insensitive).
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

    public void Save() => Zip!.Save();

    public static string HumanSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < units.Length - 1) { order++; len /= 1024; }
        return $"{len:0.##} {units[order]}";
    }
}
