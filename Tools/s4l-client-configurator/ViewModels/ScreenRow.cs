using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S4LClientConfigurator.Services;

namespace S4LClientConfigurator.ViewModels;

/// <summary>One configurable client screen: target path, preview, and import actions.</summary>
public partial class ScreenRow : ObservableObject
{
    private static readonly string[] ImageExts = { ".dds", ".tga", ".png", ".jpg", ".jpeg", ".bmp" };
    private static readonly string[] MovieExts = { ".avi", ".bik", ".bik2", ".wmv", ".mpg", ".mpeg", ".smk", ".ivf", ".usm", ".xmv", ".bin" };

    private readonly ResourceService _service;
    private readonly IClientUi _ui;
    private readonly Action _onChanged;

    public ScreenRow(ResourceService service, IClientUi ui, string displayName, Action onChanged)
    {
        _service = service;
        _ui = ui;
        _onChanged = onChanged;
        DisplayName = displayName;
        SetFromFileCommand = new AsyncRelayCommand(SetFromFileAsync);
        BrowseArchiveCommand = new AsyncRelayCommand(BrowseArchiveAsync);
    }

    public string DisplayName { get; }

    private string _resourcePath = "";
    public string ResourcePath
    {
        get => _resourcePath;
        set
        {
            value = value.Replace('\\', '/');
            if (!SetProperty(ref _resourcePath, value)) return;
            OnPropertyChanged(nameof(IsMovie));
            _onChanged();
            RefreshPreview();
        }
    }

    public bool IsMovie
    {
        get
        {
            var ext = Path.GetExtension(_resourcePath).ToLowerInvariant();
            return MovieExts.Contains(ext);
        }
    }

    private Bitmap? _thumbnail;
    public Bitmap? Thumbnail { get => _thumbnail; private set => SetProperty(ref _thumbnail, value); }

    private string _info = "Open a resource.s4hd file first.";
    public string Info { get => _info; private set => SetProperty(ref _info, value); }

    public IAsyncRelayCommand SetFromFileCommand { get; }
    public IAsyncRelayCommand BrowseArchiveCommand { get; }

    /// <summary>Loads a remembered path from settings without triggering a settings save.</summary>
    public void LoadPath(string path)
    {
        _resourcePath = path.Replace('\\', '/');
        OnPropertyChanged(nameof(ResourcePath));
        OnPropertyChanged(nameof(IsMovie));
        RefreshPreview();
    }

    private async Task SetFromFileAsync()
    {
        if (_service.Zip is null)
        {
            await _ui.ShowMessageAsync("No archive", "Open a resource.s4hd file first.");
            return;
        }
        if (string.IsNullOrWhiteSpace(ResourcePath))
        {
            await _ui.ShowMessageAsync("No target", "Set the resource path for this screen first (or use Browse…).");
            return;
        }

        var picked = await _ui.PickFilesAsync("Choose the file to import", null, null);
        if (picked.Count == 0) return;

        var bytes = await File.ReadAllBytesAsync(picked[0]);
        _service.AddOrReplace(ResourcePath, bytes);
        await _ui.ShowMessageAsync("Replaced",
            $"'{ResourcePath}' now uses\n{picked[0]}\n\nPress Save to write it back to resource.s4hd.");
        RefreshPreview();
    }

    private async Task BrowseArchiveAsync()
    {
        if (_service.Zip is null)
        {
            await _ui.ShowMessageAsync("No archive", "Open a resource.s4hd file first.");
            return;
        }

        var exts = ImageExts.Concat(MovieExts).ToList();
        var options = _service.Zip.Values
            .Where(e => exts.Contains(Path.GetExtension(e.FullName).ToLowerInvariant()))
            .Select(e => e.FullName)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (options.Count == 0)
        {
            await _ui.ShowMessageAsync("Nothing found", "No image/movie entries in this archive.");
            return;
        }

        var selected = await _ui.PickFromListAsync($"Pick an archive entry for \"{DisplayName}\"", options);
        if (!string.IsNullOrEmpty(selected))
            ResourcePath = selected;
    }

    public void RefreshPreview()
    {
        Thumbnail = null;

        if (_service.Zip is null) { Info = "Open a resource.s4hd file first."; return; }
        if (string.IsNullOrWhiteSpace(ResourcePath)) { Info = "No resource path set."; return; }
        if (!_service.Zip.TryGetValue(ResourcePath, out var entry))
        {
            Info = $"Not found in archive:\n{ResourcePath}";
            return;
        }

        if (IsMovie)
        {
            Info = $"{entry.Name}\n{ResourceService.HumanSize(entry.Length)}";
            return;
        }

        try
        {
            var bytes = entry.GetData();
            var bmp = ImageLoader.TryLoad(bytes, Path.GetExtension(ResourcePath));
            Thumbnail = bmp;
            Info = bmp is null
                ? $"{entry.Name} — no preview"
                : $"{entry.Name}  ({bmp.PixelSize.Width}×{bmp.PixelSize.Height})";
        }
        catch
        {
            Info = $"Could not decode {entry.Name}";
        }
    }
}
