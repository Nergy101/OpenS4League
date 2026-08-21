using System.Diagnostics;

namespace S4LResourceTool.App.Services;

/// <summary>Opens a file/folder with the OS default handler on Windows, macOS and Linux.</summary>
public static class PlatformOpen
{
    public static void Open(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", ["--", path]);
        }
        else
        {
            Process.Start("xdg-open", [path]);
        }
    }
}
