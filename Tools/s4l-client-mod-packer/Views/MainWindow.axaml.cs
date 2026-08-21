using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using S4League.Resource;

namespace S4LClientModPacker.Views;

public partial class MainWindow : Window
{
    private S4Zip? _source;
    private readonly List<string> _allPaths = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void BrowseSource_Click(object? sender, RoutedEventArgs e)
    {
        var p = await Pick();
        if (p is not null) SourcePath.Text = p;
    }

    private async void BrowseTarget_Click(object? sender, RoutedEventArgs e)
    {
        var p = await Pick();
        if (p is not null) TargetPath.Text = p;
    }

    private async Task<string?> Pick()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open resource.s4hd",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("S4 archive") { Patterns = new[] { "resource.s4hd" } } }
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private void LoadSource_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SourcePath.Text)) { InfoText.Text = "Pick a source archive."; return; }
        try
        {
            _source = S4Zip.OpenZip(SourcePath.Text);
            _allPaths.Clear();
            _allPaths.AddRange(_source.Values.OrderBy(x => x.FullName).Select(x => x.FullName));
            EntryList.ItemsSource = _allPaths;
            InfoText.Text = $"Loaded {_source.Count} entries.";
        }
        catch (Exception ex) { InfoText.Text = $"Load failed: {ex.Message}"; }
    }

    private void BuildMod_Click(object? sender, RoutedEventArgs e)
    {
        if (_source is null) { InfoText.Text = "Load a source archive first."; return; }
        var selected = SelectedPaths();
        if (selected.Count == 0) { InfoText.Text = "Select entries to bundle."; return; }

        var file = StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Build mod package",
            DefaultExtension = "s4mod",
            FileTypeChoices = new[] { new FilePickerFileType("S4 mod package") { Patterns = new[] { "*.s4mod" } } },
            SuggestedFileName = "clientmod.s4mod"
        });
        _ = DoBuild(file, selected);
    }

    private async Task DoBuild(Task<IStorageFile?> fileTask, List<string> selected)
    {
        var file = await fileTask;
        var path = file?.TryGetLocalPath();
        if (path is null) return;
        try
        {
            using var ms = new MemoryStream();
            using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var p in selected)
                {
                    var entry = _source![p];
                    if (entry is null) continue;
                    var part = zip.CreateEntry("files/" + p.Replace('\\', '/'));
                    using var es = part.Open();
                    es.Write(entry.GetData());
                }
                var manifest = new
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    format = "s4mod-v1",
                    created = DateTime.UtcNow.ToString("O"),
                    entries = selected.Select(p => new { path = p }).ToList()
                };
                var mp = zip.CreateEntry("manifest.json");
                using var ms2 = mp.Open();
                ms2.Write(JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions { WriteIndented = true }));
            }
            File.WriteAllBytes(path, ms.ToArray());
            InfoText.Text = $"Mod package written to {path} ({selected.Count} entries).";
        }
        catch (Exception ex) { InfoText.Text = $"Build failed: {ex.Message}"; }
    }

    private void InstallMod_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TargetPath.Text)) { InfoText.Text = "Pick an install target client first."; return; }
        var file = StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open .s4mod package",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("S4 mod package") { Patterns = new[] { "*.s4mod" } } }
        });
        _ = DoInstall(file);
    }

    private async Task DoInstall(Task<IReadOnlyList<IStorageFile>> fileTask)
    {
        var files = await fileTask;
        if (files.Count == 0) return;
        var modPath = files[0].TryGetLocalPath();
        if (modPath is null) return;
        if (string.IsNullOrEmpty(TargetPath.Text)) { InfoText.Text = "Pick an install target client first."; return; }
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(modPath);
            var target = S4Zip.OpenZip(TargetPath.Text);
            int count = 0;
            foreach (var part in archive.Entries)
            {
                if (!part.FullName.StartsWith("files/", StringComparison.OrdinalIgnoreCase) || part.FullName.EndsWith("/")) continue;
                var rel = part.FullName["files/".Length..];
                using var es = part.Open();
                using var ms = new MemoryStream();
                es.CopyTo(ms);
                var data = ms.ToArray();
                if (target.ContainsKey(rel)) target[rel]!.SetData(data);
                else target.CreateEntry(rel, data);
                count++;
            }
            target.Save();
            InfoText.Text = $"Installed {count} entries into {Path.GetFileName(TargetPath.Text)}.";
        }
        catch (Exception ex) { InfoText.Text = $"Install failed: {ex.Message}"; }
    }

    private void LoadMod_Click(object? sender, RoutedEventArgs e)
    {
        var file = StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open .s4mod package",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("S4 mod package") { Patterns = new[] { "*.s4mod" } } }
        });
        _ = ShowModContents(file);
    }

    private async Task ShowModContents(Task<IReadOnlyList<IStorageFile>> fileTask)
    {
        var files = await fileTask;
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path is null) return;
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(path);
            var contents = archive.Entries
                .Where(e => e.FullName.StartsWith("files/", StringComparison.OrdinalIgnoreCase) && !e.FullName.EndsWith("/"))
                .Select(e => "  " + e.FullName)
                .ToList();
            var manifest = archive.Entries.FirstOrDefault(e => e.FullName == "manifest.json");
            var title = Path.GetFileName(path);
            if (manifest is not null)
            {
                using var es = manifest.Open();
                using var sr = new System.IO.StreamReader(es);
                var txt = sr.ReadToEnd();
                try
                {
                    using var doc = JsonDocument.Parse(txt);
                    if (doc.RootElement.TryGetProperty("name", out var n)) title = n.GetString() ?? title;
                }
                catch { /* ignore */ }
            }
            EntryList.ItemsSource = contents;
            InfoText.Text = $"Mod '{title}': {contents.Count} entries loaded for preview (source not opened).";
        }
        catch (Exception ex) { InfoText.Text = $"Load failed: {ex.Message}"; }
    }

    private List<string> SelectedPaths()
    {
        return EntryList.SelectedItems?.Cast<string>().ToList() ?? new List<string>();
    }
}
