using System.Collections.ObjectModel;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using S4League.Resource;

namespace S4LItemEditor.Views;

public partial class MainWindow : Window
{
    private XDocument? _doc;
    private XElement? _root;
    private string? _fileName;
    private S4Zip? _zip;
    private string? _zipEntryName;
    private string _recordName = "";
    private readonly ObservableCollection<Row> _rows = new();

    public MainWindow()
    {
        InitializeComponent();
        Grid.ItemsSource = _rows;
    }

    private async void BrowseSource_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open XML data file",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("XML") { Patterns = new[] { "*.xml", "*.txt" } } }
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } p) SourcePath.Text = p;
    }

    private async void FromArchive_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open resource.s4hd",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("S4 archive") { Patterns = new[] { "resource.s4hd" } } }
        });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path is null) return;
        try
        {
            _zip = S4Zip.OpenZip(path);
            SourcePath.Text = path;
            InfoText.Text = $"Archive opened ({_zip.Count} entries). Pick an XML entry via 'Load from archive…'.";
            _doc = null; _root = null;
        }
        catch (Exception ex) { InfoText.Text = $"Open failed: {ex.Message}"; }
    }

    private void Load_Click(object? sender, RoutedEventArgs e)
    {
        if (_zip is not null && string.IsNullOrEmpty(SourcePath.Text))
        {
            // Choose an entry from the archive.
            var files = StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose a data entry",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Data") { Patterns = new[] { "*.xml", "*.txt", "*.ini" } } }
            });
            _ = PickEntry(files);
            return;
        }
        if (File.Exists(SourcePath.Text))
        {
            try { LoadXml(File.ReadAllText(SourcePath.Text), Path.GetFileName(SourcePath.Text)); }
            catch (Exception ex) { InfoText.Text = $"Load failed: {ex.Message}"; }
        }
        else InfoText.Text = "Set a file path (or open an archive and pick an entry).";
    }

    private async Task PickEntry(Task<IReadOnlyList<IStorageFile>> task)
    {
        var files = await task;
        if (files.Count == 0) return;
        // File picker can't list archive entries; load the first XML-looking entry instead.
        var candidates = _zip!.Values.Where(x =>
            x.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
            x.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.FullName).ToList();
        if (candidates.Count == 0) { InfoText.Text = "No XML/text entries in archive."; return; }
        var entry = candidates[0];
        LoadXml(System.Text.Encoding.UTF8.GetString(entry.GetData()), entry.FullName);
        _zipEntryName = entry.FullName;
        InfoText.Text = $"Loaded '{entry.FullName}' from archive.";
    }

    private void LoadXml(string text, string name)
    {
        _doc = XDocument.Parse(text);
        _root = _doc.Root;
        _fileName = name;
        var recordCandidates = _root!.Elements().GroupBy(e => e.Name.LocalName)
            .OrderByDescending(g => g.Count()).Select(g => g.Key).ToList();
        RecordCombo.ItemsSource = recordCandidates;
        _recordName = recordCandidates.FirstOrDefault() ?? "";
        RecordCombo.SelectedItem = _recordName;
        RawXml.Text = text;
        RebuildGrid();
        InfoText.Text = $"Loaded {name} — root <{_root.Name.LocalName}>, record element '{_recordName}'.";
    }

    private void RecordCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RecordCombo.SelectedItem is string s) { _recordName = s; RebuildGrid(); }
    }

    private void RebuildGrid()
    {
        if (_root is null || string.IsNullOrEmpty(_recordName)) { _rows.Clear(); Grid.Columns.Clear(); RowCount.Text = ""; return; }
        var records = _root.Elements().Where(e => e.Name.LocalName == _recordName).ToList();
        var attrNames = records.SelectMany(r => r.Attributes().Select(a => a.Name.LocalName))
            .Distinct().OrderBy(x => x).ToList();

        Grid.Columns.Clear();
        Grid.Columns.Add(new DataGridTextColumn { Header = "#", Width = new DataGridLength(42, DataGridLengthUnitType.Pixel), Binding = new Binding("Index") });
        foreach (var a in attrNames)
        {
            Grid.Columns.Add(new DataGridTextColumn
            {
                Header = a,
                Width = new DataGridLength(120, DataGridLengthUnitType.Pixel),
                Binding = new Binding($"Values[{a}]") { Mode = BindingMode.TwoWay }
            });
        }

        _rows.Clear();
        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            var dict = r.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.OrdinalIgnoreCase);
            _rows.Add(new Row(i, dict));
        }
        RowCount.Text = $"{records.Count} rows · {attrNames.Count} columns";
    }

    private void AddRow_Click(object? sender, RoutedEventArgs e)
    {
        if (_root is null || string.IsNullOrEmpty(_recordName)) { InfoText.Text = "Load a file first."; return; }
        var el = new XElement(_recordName);
        _root.Add(el);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _rows.Add(new Row(_rows.Count, dict));
        InfoText.Text = "Added an empty row (attributes appear after you edit them).";
    }

    private void DeleteRow_Click(object? sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) { InfoText.Text = "Select a row."; return; }
        if (_root is null) return;
        var targets = _root.Elements().Where(el => el.Name.LocalName == _recordName).ToList();
        if (row.Index < targets.Count) targets[row.Index].Remove();
        RebuildGrid();
        InfoText.Text = "Deleted row.";
    }

    private void Commit_Click(object? sender, RoutedEventArgs e)
    {
        if (_root is null || string.IsNullOrEmpty(_recordName)) { InfoText.Text = "Load a file first."; return; }
        var records = _root.Elements().Where(el => el.Name.LocalName == _recordName).ToList();
        for (int i = 0; i < records.Count && i < _rows.Count; i++)
        {
            var el = records[i];
            el.Attributes().Remove();
            foreach (var kv in _rows[i].Values)
                el.SetAttributeValue(kv.Key, kv.Value);
        }
        RawXml.Text = _doc!.ToString();
        InfoText.Text = "Committed grid edits into the XML (below).";
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        // Ensure edits are reflected in the serialized doc.
        Commit_Click(sender, e);
        if (_doc is null) { InfoText.Text = "Nothing to save."; return; }
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(_doc.ToString());
            if (_zip is not null && _zipEntryName is not null)
            {
                var entry = _zip[_zipEntryName];
                if (entry is not null) { entry.SetData(bytes); _zip.Save(); InfoText.Text = $"Saved into archive entry '{_zipEntryName}'."; return; }
            }
            var file = StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save XML data",
                DefaultExtension = "xml",
                SuggestedFileName = _fileName ?? "data.xml"
            });
            _ = WriteFile(file, bytes);
        }
        catch (Exception ex) { InfoText.Text = $"Save failed: {ex.Message}"; }
    }

    private async Task WriteFile(Task<IStorageFile?> task, byte[] bytes)
    {
        var file = await task;
        var path = file?.TryGetLocalPath();
        if (path is null) return;
        File.WriteAllBytes(path, bytes);
        InfoText.Text = $"Saved to {path}";
    }

    public sealed class Row
    {
        public int Index { get; }
        public Dictionary<string, string> Values { get; }
        public Row(int index, Dictionary<string, string> values) { Index = index; Values = values; }
    }
}
