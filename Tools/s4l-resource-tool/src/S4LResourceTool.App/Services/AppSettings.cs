using System.Text.Json;

namespace S4LResourceTool.App.Services;

/// <summary>Small JSON-backed settings store (remembers the last client directory).</summary>
public sealed class AppSettings
{
    public string? ClientPath { get; set; }
    public bool ConfirmReplacements { get; set; } = true;

    /// <summary>Optional Unity editor executable (e.g. .../Editor/Unity.exe) used by "Open in Unity".</summary>
    public string? UnityExecutablePath { get; set; }

    /// <summary>Optional UnityScnTool project folder where .scn files are dropped for editing.</summary>
    public string? UnityScnProjectPath { get; set; }

    /// <summary>
    /// Optional path to <c>realesrgan-ncnn-vulkan.exe</c> used for AI (Real-ESRGAN) texture
    /// preview upscaling. If unset, the app auto-detects a well-known install location.
    /// </summary>
    public string? RealesrganPath { get; set; }

    /// <summary>
    /// Optional path to <c>texconv.exe</c> (DirectXTex) used to export upscaled previews as
    /// BC7 DDS. If unset, the app auto-detects a well-known install location.
    /// </summary>
    public string? TexconvPath { get; set; }

    private static string SettingsPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "S4LResourceTool");
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
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort */ }
    }
}
