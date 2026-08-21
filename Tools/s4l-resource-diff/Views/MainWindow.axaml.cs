using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using S4League.Resource;

namespace S4LResourceDiff.Views;

public partial class MainWindow : Window
{
    private S4Zip? _base;
    private S4Zip? _other;
    private readonly List<DiffEntry> _diff = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void BrowseBase_Click(object? sender, RoutedEventArgs e)
    {
        var p = await Pick();
        if (p is not null) BasePath.Text = p;
    }

    private async void BrowseOther_Click(object? sender, RoutedEventArgs e)
    {
        var p = await Pick();
        if (p is not null) OtherPath.Text = p;
    }

    private void Swap_Click(object? sender, RoutedEventArgs e)
    {
        (BasePath.Text, OtherPath.Text) = (OtherPath.Text, BasePath.Text);
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

    private void Compare_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(BasePath.Text) || string.IsNullOrEmpty(OtherPath.Text))
        { InfoText.Text = "Pick both archives first."; return; }
        try
        {
            _base = S4Zip.OpenZip(BasePath.Text);
            _other = S4Zip.OpenZip(OtherPath.Text);
            RunDiff();
        }
        catch (Exception ex) { InfoText.Text = $"Compare failed: {ex.Message}"; }
    }

    private void RunDiff()
    {
        _diff.Clear();
        var baseEntries = _base!.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var otherEntries = _other!.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var kv in otherEntries)
        {
            if (!baseEntries.ContainsKey(kv.Key))
            {
                _diff.Add(new DiffEntry(kv.Key, "added", kv.Value.Length));
            }
            else if (!SameData(baseEntries[kv.Key], kv.Value))
            {
                _diff.Add(new DiffEntry(kv.Key, "changed", kv.Value.Length));
            }
        }
        foreach (var kv in baseEntries)
        {
            if (!otherEntries.ContainsKey(kv.Key))
                _diff.Add(new DiffEntry(kv.Key, "removed", kv.Value.Length));
        }

        _diff.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));

        ResultList.ItemsSource = _diff.Select(d => $"[{d.Status,-7}] {d.Path}  ({d.Size} B)").ToList();

        SumAdded.Text = $"+ {_diff.Count(d => d.Status == "added")} added";
        SumRemoved.Text = $"- {_diff.Count(d => d.Status == "removed")} removed";
        SumChanged.Text = $"~ {_diff.Count(d => d.Status == "changed")} changed";
        SumTotal.Text = $"total {_diff.Count} diff entries";

        InfoText.Text = "Diff complete.";
    }

    private static bool SameData(S4ZipEntry a, S4ZipEntry b)
    {
        if (a.Length != b.Length) return false;
        try { return a.Checksum == b.Checksum || a.GetData().AsSpan().SequenceEqual(b.GetData()); }
        catch { return false; }
    }

    private void ExportDelta_Click(object? sender, RoutedEventArgs e)
    {
        if (_other is null || _diff.Count == 0) { InfoText.Text = "Nothing to export — compare first."; return; }
        var file = StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export delta package",
            DefaultExtension = "s4delta",
            FileTypeChoices = new[] { new FilePickerFileType("S4 delta package") { Patterns = new[] { "*.s4delta" } } },
            SuggestedFileName = "delta.s4delta"
        });
        _ = DoExport(file);
    }

    private async Task DoExport(Task<IStorageFile?> fileTask)
    {
        var file = await fileTask;
        var path = file?.TryGetLocalPath();
        if (path is null) return;
        try
        {
            using var ms = new MemoryStream();
            using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var d in _diff)
                {
                    if (d.Status == "removed") continue;
                    var entry = _other![d.Path];
                    if (entry is null) continue;
                    var part = zip.CreateEntry("files/" + d.Path.Replace('\\', '/'));
                    using var es = part.Open();
                    es.Write(entry.GetData());
                }
                var manifest = new
                {
                    base_archive = Path.GetFileName(BasePath.Text),
                    other_archive = Path.GetFileName(OtherPath.Text),
                    created = DateTime.UtcNow.ToString("O"),
                    entries = _diff.Select(d => new { path = d.Path, status = d.Status }).ToList()
                };
                var mp = zip.CreateEntry("manifest.json");
                using var ms2 = mp.Open();
                var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions { WriteIndented = true });
                ms2.Write(bytes);
            }
            File.WriteAllBytes(path, ms.ToArray());
            InfoText.Text = $"Exported delta to {path}";
        }
        catch (Exception ex) { InfoText.Text = $"Export failed: {ex.Message}"; }
    }

    private void CopyBtoA_Click(object? sender, RoutedEventArgs e)
    {
        if (_base is null || _other is null || _diff.Count == 0) { InfoText.Text = "Compare first."; return; }
        try
        {
            int applied = 0;
            foreach (var d in _diff)
            {
                if (d.Status == "removed") continue;
                var entry = _other[d.Path];
                if (entry is null) continue;
                var data = entry.GetData();
                if (_base.ContainsKey(d.Path))
                    _base[d.Path]!.SetData(data);
                else
                    _base.CreateEntry(d.Path, data);
                applied++;
            }
            _base.Save();
            InfoText.Text = $"Applied {applied} changes into base archive (B → A).";
        }
        catch (Exception ex) { InfoText.Text = $"Apply failed: {ex.Message}"; }
    }

    private sealed record DiffEntry(string Path, string Status, int Size);
}
