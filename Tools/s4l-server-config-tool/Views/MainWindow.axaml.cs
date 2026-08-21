using System.IO.Compression;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace S4LServerConfigTool.Views;

public partial class MainWindow : Window
{
    private string? _repoRoot;
    private string? _currentFile;
    private readonly List<(string Name, string File)> _servers = new();
    private readonly List<string> _plugins = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Detect_Click(object? sender, RoutedEventArgs e)
    {
        // Walk up from the app's location or the CWD looking for Server/opens4l.
        var candidates = new List<string>
        {
            Environment.CurrentDirectory,
            Path.GetDirectoryName(Environment.ProcessPath) ?? ""
        };
        foreach (var start in candidates)
        {
            var root = FindRepo(start);
            if (root is not null) { LoadRepo(root); return; }
        }
        InfoText.Text = "Could not auto-detect the OpenS4L repo. Run the tool from inside the repo.";
    }

    private static string? FindRepo(string start)
    {
        var dir = start;
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, "Server", "opens4l"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private void LoadRepo(string root)
    {
        _repoRoot = root;
        _servers.Clear();
        _plugins.Clear();

        var src = Path.Combine(root, "Server", "opens4l", "src");
        foreach (var name in new[] { "Auth", "Chat", "Game", "Relay" })
        {
            var file = Path.Combine(src, $"OpenS4L.Server.{name}", "config.hjson");
            if (File.Exists(file)) _servers.Add((name, file));
        }

        var pluginRoot = Path.Combine(src, "plugins");
        if (Directory.Exists(pluginRoot))
        {
            foreach (var d in Directory.GetDirectories(pluginRoot))
                if (File.Exists(Path.Combine(d, $"{Path.GetFileName(d)}.csproj")))
                    _plugins.Add(Path.GetFileName(d));
        }

        ServerList.ItemsSource = _servers.Select(s => s.Name).ToList();
        PluginList.ItemsSource = _plugins.Select(p => $"{p}").ToList();
        InfoText.Text = $"Repo detected: {root} — {_servers.Count} configs, {_plugins.Count} plugins.";
        if (_servers.Count > 0) ServerList.SelectedIndex = 0;
    }

    private void ServerList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        int idx = ServerList.SelectedIndex;
        if (idx < 0 || idx >= _servers.Count) return;
        var s = _servers[idx];
        _currentFile = s.File;
        EditorTitle.Text = $"{s.Name} — config.hjson";
        PathInfo.Text = s.File;
        try { Editor.Text = File.ReadAllText(s.File); }
        catch (Exception ex) { Editor.Text = $"<error: {ex.Message}>"; }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentFile is null) { InfoText.Text = "Select a server config first."; return; }
        try
        {
            File.WriteAllText(_currentFile, Editor.Text ?? "", new UTF8Encoding(false));
            InfoText.Text = $"Saved {Path.GetFileName(_currentFile)}.";
        }
        catch (Exception ex) { InfoText.Text = $"Save failed: {ex.Message}"; }
    }

    private void Deploy_Click(object? sender, RoutedEventArgs e)
    {
        if (_repoRoot is null) { InfoText.Text = "Detect the repo first."; return; }
        var file = StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Build deploy package",
            DefaultExtension = "zip",
            FileTypeChoices = new[] { new FilePickerFileType("Zip") { Patterns = new[] { "*.zip" } } },
            SuggestedFileName = "opens4l-deploy.zip"
        });
        _ = DoDeploy(file);
    }

    private async Task DoDeploy(Task<IStorageFile?> fileTask)
    {
        var file = await fileTask;
        var outPath = file?.TryGetLocalPath();
        if (outPath is null) return;
        try
        {
            // Commit any unsaved edits first.
            if (_currentFile is not null) File.WriteAllText(_currentFile, Editor.Text ?? "", new UTF8Encoding(false));

            using var fs = new FileStream(outPath, FileMode.Create);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

            var src = Path.Combine(_repoRoot!, "Server", "opens4l", "src");
            foreach (var s in _servers)
            {
                zip.CreateEntryFromFile(s.File, $"config/{s.Name}/config.hjson");
            }
            // Bundle each plugin's project folder (source + its config) so deploy can rebuild/place it.
            var pluginRoot = Path.Combine(src, "plugins");
            foreach (var plugin in _plugins)
            {
                var dir = Path.Combine(pluginRoot, plugin);
                AddDirectory(zip, dir, $"plugins/{plugin}");
            }
            InfoText.Text = $"Deploy package written to {outPath}.";
        }
        catch (Exception ex) { InfoText.Text = $"Deploy failed: {ex.Message}"; }
    }

    private static void AddDirectory(ZipArchive zip, string dir, string prefix)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir))
        {
            var name = Path.GetFileName(file);
            if (name is "bin" or "obj" or ".git") continue;
            zip.CreateEntryFromFile(file, $"{prefix}/{name}");
        }
    }
}
