using System.Text.Json;

namespace S4LClientConfigurator.Services;

/// <summary>
/// Small JSON-backed settings store. Remembers the last client directory and the
/// display-name -> archive-entry-path mapping for each configurable screen.
/// </summary>
public sealed class AppSettings
{
    public string? ClientPath { get; set; }

    /// <summary>Display name -> resource.s4hd entry path (the mapping the user configures).</summary>
    public Dictionary<string, string> ScreenPaths { get; set; } = new();

    private static string SettingsPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "S4LClientConfigurator");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch { /* ignore corrupt settings */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort */ }
    }
}
