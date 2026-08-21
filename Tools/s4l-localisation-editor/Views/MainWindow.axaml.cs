using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using S4League.Resource;

namespace S4LLocalisationEditor.Views;

public partial class MainWindow : Window
{
    private S4Zip? _zip;
    private S4ZipEntry? _current;
    private readonly Dictionary<string, string> _textCache = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void BrowseArchive_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open resource.s4hd",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("S4 archive") { Patterns = new[] { "resource.s4hd" } } }
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } p) ArchivePath.Text = p;
    }

    private void OpenArchive_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ArchivePath.Text)) { InfoText.Text = "Pick an archive."; return; }
        try
        {
            _zip = S4Zip.OpenZip(ArchivePath.Text);
            _textCache.Clear();
            var locales = _zip.Values
                .Where(x => IsLanguage(x.FullName))
                .OrderBy(x => x.FullName)
                .Select(x => x.FullName)
                .ToList();
            LocaleCombo.ItemsSource = locales;
            InfoText.Text = $"Opened {Path.GetFileName(ArchivePath.Text)} — {locales.Count} language file(s).";
            if (locales.Count > 0) LocaleCombo.SelectedIndex = 0;
        }
        catch (Exception ex) { InfoText.Text = $"Open failed: {ex.Message}"; }
    }

    private static bool IsLanguage(string fullName)
    {
        var n = fullName.ToLowerInvariant();
        if (n.StartsWith("+language/", StringComparison.OrdinalIgnoreCase)) return true;
        if (n.Contains("language")) return true;
        return n.EndsWith(".x7", StringComparison.OrdinalIgnoreCase) && (n.Contains("str") || n.Contains("text") || n.Contains("lang"));
    }

    private void LocaleCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_zip is null || LocaleCombo.SelectedItem is not string fullName) return;
        var entry = _zip[fullName];
        if (entry is null) return;
        _current = entry;
        Editor.Text = DecodeText(entry);
        LocaleInfo.Text = $"{entry.FullName}  ·  {entry.Length} bytes";
        InfoText.Text = "";
    }

    private string DecodeText(S4ZipEntry entry)
    {
        if (_textCache.TryGetValue(entry.FullName, out var cached)) return cached;
        try
        {
            var data = entry.GetData();
            // Try UTF-8, else fall back to the system ANSI codepage (legacy S4 files are often
            // single-byte encoded, e.g. Windows-1252). x7 payloads are already decrypted by GetData().
            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(data);
            }
            catch (DecoderFallbackException)
            {
                text = Encoding.GetEncoding(0).GetString(data);
            }
            // Strip trailing nulls.
            text = text.TrimEnd('\0');
            _textCache[entry.FullName] = text;
            return text;
        }
        catch
        {
            return "";
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_current is null) { InfoText.Text = "Select a language file first."; return; }
        try
        {
            var text = Editor.Text ?? "";
            var data = Encoding.UTF8.GetBytes(text);
            _current.SetData(data);
            _textCache[_current.FullName] = text;
            InfoText.Text = $"Saved '{_current.FullName}' ({data.Length} bytes). Remember to Save the archive via a resource editor.";
        }
        catch (Exception ex) { InfoText.Text = $"Save failed: {ex.Message}"; }
    }

    // ---- Key diffing -------------------------------------------------------

    private static Dictionary<string, string> ParseKeys(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();
            if (key.Length > 0) map[key] = val;
        }
        return map;
    }

    private void Diff_Click(object? sender, RoutedEventArgs e)
    {
        if (_zip is null || LocaleCombo.ItemsSource is not IReadOnlyList<string> locales) return;
        if (locales.Count < 2) { InfoText.Text = "Need at least two locale files."; return; }

        var sb = new StringBuilder();
        // Use the first file's keys as the reference; report which other locales are missing each.
        var refEntry = _zip[locales[0]];
        if (refEntry is null) return;
        var refKeys = ParseKeys(DecodeText(refEntry));
        sb.AppendLine($"Missing keys relative to '{refEntry.FullName}' ({refKeys.Count} keys):");
        foreach (var loc in locales.Skip(1))
        {
            var entry = _zip[loc];
            if (entry is null) continue;
            var keys = ParseKeys(DecodeText(entry));
            var missing = refKeys.Keys.Where(k => !keys.ContainsKey(k)).ToList();
            sb.AppendLine($"\n[{entry.FullName}] {missing.Count} missing:");
            foreach (var k in missing.Take(100)) sb.AppendLine($"    {k}");
            if (missing.Count > 100) sb.AppendLine($"    …and {missing.Count - 100} more");
        }
        Editor.Text = sb.ToString();
        _current = null;
        LocaleInfo.Text = "Missing-key report (not an editable file)";
        InfoText.Text = "Report generated.";
    }

    private void FindMissing_Click(object? sender, RoutedEventArgs e)
    {
        if (_zip is null || _current is null || LocaleCombo.ItemsSource is not IReadOnlyList<string> locales)
        { InfoText.Text = "Select a locale file first."; return; }

        // Reference = the union of all keys except the current file's own.
        var reference = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var loc in locales)
        {
            if (string.Equals(loc, _current.FullName, StringComparison.OrdinalIgnoreCase)) continue;
            var entry = _zip[loc];
            if (entry is null) continue;
            foreach (var k in ParseKeys(DecodeText(entry)).Keys) reference.Add(k);
        }

        var currentText = Editor.Text ?? "";
        var currentKeys = ParseKeys(currentText);
        var lines = currentText.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        foreach (var k in reference)
        {
            if (!currentKeys.ContainsKey(k))
                lines.Add($"{k} = ");
        }

        Editor.Text = string.Join("\n", lines);
        _current.SetData(Encoding.UTF8.GetBytes(Editor.Text));
        InfoText.Text = $"Appended {reference.Count(k => !currentKeys.ContainsKey(k))} missing key(s).";
    }
}
