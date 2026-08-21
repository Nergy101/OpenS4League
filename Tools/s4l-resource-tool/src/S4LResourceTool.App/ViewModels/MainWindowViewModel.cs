using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S4League.Resource;
using S4LResourceTool.App.Services;

namespace S4LResourceTool.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".xml", ".ini", ".x7", ".lua", ".cfg", ".lst", ".csv", ".json", ".html", ".htm" };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".dds", ".tga", ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    private static readonly HashSet<string> SceneExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".scn" };

    private readonly ResourceService _resources = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "S4LResourceTool");
    private readonly Dictionary<string, (S4ZipEntry Entry, DateTime LastWrite)> _tracked = new();
    private DispatcherTimer? _watchTimer;

    /// <summary>Set by the view once the window exists.</summary>
    public IUiServices? Ui { get; set; }

    /// <summary>Maintained by the view's DataGrid selection.</summary>
    public List<ResourceRow> SelectedRows { get; } = new();

    public ObservableCollection<FolderNode> Folders { get; } = new();
    public ObservableCollection<ResourceRow> Files { get; } = new();

    [ObservableProperty] private FolderNode? _selectedFolder;
    [ObservableProperty] private ResourceRow? _selectedFile;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _status = "Select your S4 League directory to begin.";
    [ObservableProperty] private string _clientPathText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _progressVisible;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private double _progressMax = 1;

    // Preview state
    [ObservableProperty] private string? _previewText;
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private string? _previewInfo;
    [ObservableProperty] private bool _showText;
    [ObservableProperty] private bool _showImage;
    [ObservableProperty] private bool _showInfo;
    [ObservableProperty] private bool _showScene;
    [ObservableProperty] private IReadOnlyList<S4League.Scn.ScnMesh>? _previewMeshes;
    [ObservableProperty] private IReadOnlyDictionary<string, ScnTexture>? _previewTextures;
    [ObservableProperty] private bool _showTexturePicker;
    [ObservableProperty] private string? _selectedTexture;
    [ObservableProperty] private string? _previewTextureOverride;
    public ObservableCollection<string> TextureOptions { get; } = new();

    // Image preview upscale state. _previewOriginalImage is the decoded 1x bitmap kept alive
    // so switching back to 1x is free; _previewTempImage is the current upscaled copy (if any).
    private Bitmap? _previewOriginalImage;
    private Bitmap? _previewTempImage;
    private byte[]? _previewSource; // raw BGRA of the selected DDS/TGA
    private int _previewSourceW;
    private int _previewSourceH;
    private int _upscaleToken;

    // The most recent upscaled raw BGRA (used by "Export upscaled…"); null unless a 4x/8x
    // preview is currently showing.
    private byte[]? _previewUpscaledBgra;
    private int _previewUpscaledW;
    private int _previewUpscaledH;

    /// <summary>True when a 4x/8x preview is showing and can be exported.</summary>
    [ObservableProperty] private bool _canExportUpscaled;

    /// <summary>Scale choices offered in the image preview pane (only for decodable DDS/TGA).</summary>
    public IReadOnlyList<string> PreviewScales { get; } = new[] { "1x", "4x", "8x" };

    [ObservableProperty] private string _previewScale = "1x";
    [ObservableProperty] private bool _showScalePicker;

    partial void OnPreviewScaleChanged(string value)
    {
        int scale = value switch { "4x" => 4, "8x" => 8, _ => 1 };
        RegenerateImage(scale);
    }

    partial void OnSelectedTextureChanged(string? value)
    {
        // The first option is the "Auto (per-face)" placeholder -> null override.
        PreviewTextureOverride = string.IsNullOrEmpty(value) || value == TextureOptions.FirstOrDefault()
            ? null
            : value;
    }

    public MainWindowViewModel()
    {
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>Called by the view after it has wired up <see cref="Ui"/>.</summary>
    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ClientPath) && Directory.Exists(_settings.ClientPath))
            await OpenClientAsync(_settings.ClientPath!);
    }

    partial void OnSelectedFolderChanged(FolderNode? value)
    {
        if (string.IsNullOrEmpty(SearchText) || SearchText.Length <= 2)
            RefreshFiles();
    }

    /// <summary>Latest preview-load task; exposed for headless tests to await.</summary>
    internal Task? PreviewTask { get; private set; }

    partial void OnSelectedFileChanged(ResourceRow? value) => PreviewTask = LoadPreviewAsync(value);

    partial void OnSearchTextChanged(string value) => RefreshFiles();

    // ---- Commands -----------------------------------------------------------

    [RelayCommand]
    private async Task SelectDirectoryAsync()
    {
        if (Ui is null) return;
        var dir = await Ui.PickFolderAsync("Select your S4 League directory");
        if (string.IsNullOrWhiteSpace(dir)) return;
        await OpenClientAsync(dir);
    }

    /// <summary>
    /// Scans a user-chosen directory tree for <c>resource.s4hd</c> files and lets them pick
    /// which archive to open (showing each full path). Opt-in alternative to navigating manually.
    /// </summary>
    [RelayCommand]
    private async Task ScanArchivesAsync()
    {
        if (Ui is null) return;
        var root = await Ui.PickFolderAsync("Choose a folder to scan for resource.s4hd archives");
        if (string.IsNullOrWhiteSpace(root)) return;

        try
        {
            IsBusy = true;
            Status = "Scanning for resource.s4hd…";

            // Fast path: if the chosen folder itself contains resource.s4hd, just open it.
            var direct = Path.Combine(root, "resource.s4hd");
            if (File.Exists(direct))
            {
                await OpenClientAsync(root);
                return;
            }

            var found = await Task.Run(() => ScanForArchives(root));
            if (found.Count == 0)
            {
                await Ui.ShowMessageAsync("Scan for resource.s4hd",
                    $"No resource.s4hd archives found under:\n{root}");
                return;
            }

            var options = found
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Select(p => (Label: p, Value: p))
                .ToList();

            Status = $"Found {found.Count} resource.s4hd archive(s).";
            var chosen = await Ui.PickFromListAsync(
                $"Select an archive ({found.Count} found)", options);
            if (string.IsNullOrWhiteSpace(chosen)) return;

            await OpenClientAsync(chosen);
        }
        catch (Exception ex)
        {
            await Ui.ShowMessageAsync("Scan failed", ex.Message);
            Status = "Scan failed.";
        }
        finally { IsBusy = false; }
    }

    private static List<string> ScanForArchives(string root)
    {
        var list = new List<string>();
        try
        {
            foreach (var f in Directory.EnumerateFiles(root, "resource.s4hd", SearchOption.AllDirectories))
                list.Add(f);
        }
        catch
        {
            // unreadable subdirectories are skipped; whatever we did find is returned
        }
        return list;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!_resources.IsOpen || Ui is null) return;
        try
        {
            IsBusy = true;
            Status = "Saving archive...";
            await Task.Run(() => _resources.Save());
            foreach (var row in Files) { row.IsModified = false; row.RefreshModified(); }
            Status = "Archive saved.";
        }
        catch (Exception ex)
        {
            await Ui.ShowMessageAsync("Save failed", ex.Message);
            Status = "Save failed.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task FindUnusedAsync()
    {
        if (!_resources.IsOpen || Ui is null) return;

        var result = await Task.Run(() => _resources.FindUnused());
        if (result.Files.Count == 0)
        {
            await Ui.ShowMessageAsync("Find unused resources", "No unused resources found.");
            return;
        }

        var size = ResourceService.HumanSize(result.TotalBytes);
        var ok = await Ui.ConfirmAsync("Delete unused resources",
            $"Found {result.Files.Count} unused file(s) totaling {size}.\n\n" +
            "Permanently delete them from disk?");
        if (!ok) return;

        var deleted = await Task.Run(() =>
        {
            var n = 0;
            foreach (var f in result.Files)
            {
                try { File.Delete(f); n++; } catch { /* ignore */ }
            }
            return n;
        });
        Status = $"Deleted {deleted} unused file(s), freed {size}.";
    }

    [RelayCommand]
    private void OpenExternally()
    {
        var targets = CurrentSelection();
        foreach (var row in targets)
        {
            try
            {
                var temp = Path.Combine(_tempDir, row.FullName.Replace('/', '_').Replace('\\', '_'));
                Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
                File.WriteAllBytes(temp, row.Entry.GetData());
                _tracked[temp] = (row.Entry, File.GetLastWriteTime(temp));
                PlatformOpen.Open(temp);
            }
            catch (Exception ex)
            {
                Status = $"Open failed: {ex.Message}";
            }
        }
        EnsureWatchTimer();
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Ui is null) return;
        var targets = CurrentSelection();
        if (targets.Count == 0) return;

        if (targets.Count == 1)
        {
            var entry = targets[0].Entry;
            var dest = await Ui.PickSaveFileAsync(entry.Name, Path.GetExtension(entry.Name));
            if (string.IsNullOrWhiteSpace(dest)) return;
            try
            {
                await Task.Run(() => File.WriteAllBytes(dest, entry.GetData()));
                Status = $"Exported {entry.Name}.";
            }
            catch (Exception ex) { await Ui.ShowMessageAsync("Export failed", ex.Message); }
            return;
        }

        var dir = await Ui.PickFolderAsync("Select a folder to export the selected files into");
        if (string.IsNullOrWhiteSpace(dir)) return;

        var basePath = SelectedFolder?.FullPath ?? "";
        var list = targets.ToList();
        ProgressMax = list.Count;
        ProgressValue = 0;
        ProgressVisible = true;
        Status = $"Exporting {list.Count} file(s)...";

        await Task.Run(() =>
        {
            for (var i = 0; i < list.Count; i++)
            {
                try
                {
                    var entry = list[i].Entry;
                    var relative = entry.FullName;
                    if (basePath.Length > 0 && relative.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase))
                        relative = relative[(basePath.Length + 1)..];
                    var outPath = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                    File.WriteAllBytes(outPath, entry.GetData());
                }
                catch { /* skip individual failures */ }

                var done = i + 1;
                Dispatcher.UIThread.Post(() => ProgressValue = done);
            }
        });

        ProgressVisible = false;
        Status = $"Exported {list.Count} file(s).";
    }

    [RelayCommand]
    private async Task ReplaceAsync()
    {
        if (Ui is null) return;
        var targets = CurrentSelection();
        if (targets.Count != 1) return;

        var entry = targets[0].Entry;
        var ext = Path.GetExtension(entry.Name);
        var picked = await Ui.PickFilesAsync($"Replace {entry.Name}",
            string.IsNullOrEmpty(ext) ? null : ext.TrimStart('.').ToUpperInvariant() + " files",
            string.IsNullOrEmpty(ext) ? null : ext);
        if (picked.Count == 0) return;

        try
        {
            var bytes = await File.ReadAllBytesAsync(picked[0]);
            _resources.Replace(entry, bytes);
            targets[0].IsModified = true;
            targets[0].RefreshModified();
            await LoadPreviewAsync(targets[0]);
            Status = $"Replaced {entry.Name} (remember to Save).";
        }
        catch (Exception ex) { await Ui.ShowMessageAsync("Replace failed", ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Ui is null) return;
        var targets = CurrentSelection();
        if (targets.Count == 0) return;

        var ok = await Ui.ConfirmAsync("Delete",
            $"Delete {targets.Count} item{(targets.Count == 1 ? "" : "s")} from the archive?");
        if (!ok) return;

        foreach (var row in targets)
            _resources.Delete(row.Entry);

        RefreshTree();
        RefreshFiles();
        Status = $"Deleted {targets.Count} item(s) (remember to Save).";
    }

    /// <summary>
    /// Exports the selected .scn into the UnityScnTool project and opens it in Unity for editing.
    /// Uses the configured Unity project path (or the temp dir if none is set), then launches
    /// the configured Unity executable if available.
    /// </summary>
    [RelayCommand]
    private async Task OpenInUnityAsync()
    {
        if (Ui is null) return;
        var row = SelectedFile;
        if (row is null || !SceneExtensions.Contains(Path.GetExtension(row.Name))) return;

        try
        {
            var entry = row.Entry;

            // Destination: configured Unity project's Assets folder, else a temp import folder.
            string destDir;
            if (!string.IsNullOrEmpty(_settings.UnityScnProjectPath))
            {
                destDir = Path.Combine(_settings.UnityScnProjectPath, "Assets");
                Directory.CreateDirectory(destDir);
            }
            else
            {
                destDir = Path.Combine(_tempDir, "scn-import");
                Directory.CreateDirectory(destDir);
            }

            var dest = Path.Combine(destDir, entry.Name);
            await Task.Run(() => File.WriteAllBytes(dest, entry.GetData()));

            if (!string.IsNullOrEmpty(_settings.UnityExecutablePath) && File.Exists(_settings.UnityExecutablePath))
            {
                var psi = new System.Diagnostics.ProcessStartInfo(_settings.UnityExecutablePath)
                {
                    Arguments = $"-projectPath \"{_settings.UnityScnProjectPath}\""
                };
                System.Diagnostics.Process.Start(psi);
                Status = $"Opened {entry.Name} in Unity.";
            }
            else if (!string.IsNullOrEmpty(_settings.UnityScnProjectPath))
            {
                PlatformOpen.Open(_settings.UnityScnProjectPath);
                Status = $"Exported {entry.Name} to Unity project. Open it in Unity to edit.";
            }
            else
            {
                PlatformOpen.Open(Path.GetDirectoryName(dest)!);
                Status = $"Exported {entry.Name} to {Path.GetDirectoryName(dest)}. Set a Unity project path to open it directly.";
            }
        }
        catch (Exception ex)
        {
            Status = $"Open in Unity failed: {ex.Message}";
        }
    }

    // ---- Drag & drop entry point (called by the view) -----------------------

    public async Task AddFilesAsync(IEnumerable<string> paths)
    {
        if (!_resources.IsOpen) return;
        var prefix = SelectedFolder is { FullPath.Length: > 0 } f ? f.FullPath + "/" : "";
        var added = 0;

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                added += await AddDirectoryAsync(path, prefix + Path.GetFileName(path) + "/");
            else if (File.Exists(path))
            {
                var bytes = await File.ReadAllBytesAsync(path);
                _resources.AddOrReplace(prefix + Path.GetFileName(path), bytes);
                added++;
            }
        }

        RefreshTree();
        RefreshFiles();
        Status = $"Added/replaced {added} file(s) (remember to Save).";
    }

    private async Task<int> AddDirectoryAsync(string dir, string prefix)
    {
        var added = 0;
        foreach (var entry in Directory.GetFileSystemEntries(dir))
        {
            if (Directory.Exists(entry))
                added += await AddDirectoryAsync(entry, prefix + Path.GetFileName(entry) + "/");
            else
            {
                var bytes = await File.ReadAllBytesAsync(entry);
                _resources.AddOrReplace(prefix + Path.GetFileName(entry), bytes);
                added++;
            }
        }
        return added;
    }

    // ---- Internals ----------------------------------------------------------

    private List<ResourceRow> CurrentSelection()
    {
        if (SelectedRows.Count > 0) return SelectedRows.ToList();
        return SelectedFile is null ? new List<ResourceRow>() : new List<ResourceRow> { SelectedFile };
    }

    private async Task OpenClientAsync(string dir)
    {
        if (Ui is null) return;
        try
        {
            IsBusy = true;
            Status = "Opening archive...";
            await Task.Run(() => _resources.Open(dir));

            _settings.ClientPath = dir;
            _settings.Save();
            ClientPathText = dir;

            RefreshTree();
            RefreshFiles();
            Status = $"Loaded {_resources.Zip!.Count} resources.";
        }
        catch (Exception ex)
        {
            await Ui.ShowMessageAsync("Failed to open archive", ex.Message);
            Status = "Failed to open archive.";
        }
        finally { IsBusy = false; }
    }

    private void RefreshTree()
    {
        Folders.Clear();
        if (!_resources.IsOpen) return;

        var root = new FolderNode("resource.s4hd", "") { IsExpanded = true };
        var map = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase) { [""] = root };

        FolderNode Ensure(string path)
        {
            if (map.TryGetValue(path, out var existing)) return existing;
            var parent = Ensure(ResourceService.FolderOf(path));
            var node = new FolderNode(Path.GetFileName(path), path);
            parent.Children.Add(node);
            map[path] = node;
            return node;
        }

        foreach (var path in _resources.AllFolderPaths())
            Ensure(path);

        Folders.Add(root);
        root.IsExpanded = true;
        // Auto-expand the first level of folders so the tree opens to something useful.
        foreach (var child in root.Children)
            child.IsExpanded = true;
        SelectedFolder = root;
    }

    private void RefreshFiles()
    {
        Files.Clear();
        if (!_resources.IsOpen) return;

        IEnumerable<S4ZipEntry> entries;
        if (!string.IsNullOrEmpty(SearchText) && SearchText.Length > 2)
            entries = _resources.Search(SearchText);
        else
            entries = _resources.FilesIn(SelectedFolder?.FullPath ?? "");

        foreach (var e in entries)
            Files.Add(new ResourceRow(e));
    }

    private async Task LoadPreviewAsync(ResourceRow? row)
    {
        if (row is null)
        {
            ClearPreview();
            return;
        }

        var entry = row.Entry;
        var ext = Path.GetExtension(entry.Name);

        // Free any previous image preview assets (raw source + displayed bitmaps) before
        // loading the newly selected file. Scenes/text reuse SetPreview below.
        DisposeImageAssets();

        try
        {
            var data = await Task.Run(() => entry.GetData());

            if (SceneExtensions.Contains(ext))
            {
                var meshes = await Task.Run(() => ParseScene(data, entry.Name));
                if (meshes.Count > 0)
                {
                    // Resolve referenced textures from the archive (best-effort).
                    var names = S4League.Scn.SceneMeshBuilder.DistinctTextureNames(meshes);
                    var texMap = new Dictionary<string, ScnTexture>(StringComparer.OrdinalIgnoreCase);
                    var missing = new List<string>();
                    foreach (var n in names)
                    {
                        var tex = await Task.Run(() => ScnTextureLoader.TryLoad(_resources, n));
                        if (tex is not null)
                            texMap[n] = tex;
                        else
                            missing.Add(n);
                    }

                    TextureOptions.Clear();
                    TextureOptions.Add("Auto (per-face)");
                    foreach (var n in names)
                        TextureOptions.Add(n);
                    SelectedTexture = null; // Auto

                    SetPreview(meshes: meshes, textures: texMap,
                        info: SceneSummary(entry.Name, data.Length, meshes, names.Count, texMap.Count, missing));
                    return;
                }
                SetPreview(info: $"{entry.Name}\n{ResourceService.HumanSize(entry.Length)}\n\nNo renderable meshes found in this .scn (e.g. a collision or logic-only scene).");
                return;
            }

            if (ImageExtensions.Contains(ext))
            {
                // Prefer raw BGRA so the upscale control can operate on source pixels
                // (DDS/TGA only). For other image types fall back to Avalonia's decoder.
                var raw = await Task.Run(() => ImageLoader.TryLoadRaw(data, ext));
                if (raw is not null)
                {
                    _previewSource = raw.Value.Bgra;
                    _previewSourceW = raw.Value.Width;
                    _previewSourceH = raw.Value.Height;
                    var original = ImageLoader.FromBgra(raw.Value.Bgra, raw.Value.Width, raw.Value.Height);
                    if (original is not null)
                    {
                        _previewOriginalImage = original;
                        _previewTempImage = null;
                        PreviewScale = "1x";
                        SetPreview(image: original);
                        return;
                    }
                }

                var bmp = await Task.Run(() => ImageLoader.TryLoad(data, ext));
                if (bmp is not null)
                {
                    SetPreview(image: bmp);
                    return;
                }
                SetPreview(info: $"{entry.Name}\nUnsupported image format ({ext}).\n{ResourceService.HumanSize(entry.Length)}");
                return;
            }

            if (TextExtensions.Contains(ext))
            {
                var text = Encoding.UTF8.GetString(data);
                if (text.Length > 1_000_000) text = text[..1_000_000] + "\n\n[...truncated...]";
                SetPreview(text: text);
                return;
            }

            SetPreview(info: $"{entry.Name}\n{ResourceService.HumanSize(entry.Length)}\n\nBinary file – use \"Open\" to view in an external application.");
        }
        catch (Exception ex)
        {
            SetPreview(info: $"Failed to load \"{entry.FullName}\":\n{ex.Message}");
        }
    }

    private static IReadOnlyList<S4League.Scn.ScnMesh> ParseScene(byte[] data, string name)
    {
        try
        {
            using var ms = new MemoryStream(data);
            var container = S4League.Scn.SceneContainer.ReadFrom(ms);
            return S4League.Scn.SceneMeshBuilder.Build(container);
        }
        catch (Exception ex)
        {
            throw new Exception($"{name} could not be parsed as a scene:\n{ex.Message}");
        }
    }

    private static string SceneSummary(string name, int size, IReadOnlyList<S4League.Scn.ScnMesh> meshes,
        int textureNames, int loadedTextures, List<string> missing)
    {
        var totalVerts = meshes.Sum(m => m.Vertices.Length);
        var totalTris = meshes.Sum(m => m.Indices.Length / 3);

        // Count faces by whether they carry a texture and whether it resolved.
        int texturedFaces = 0, untexturedFaces = 0, resolvedFaces = 0;
        foreach (var m in meshes)
        {
            int faces = m.Indices.Length / 3;
            for (int f = 0; f < faces && f < m.FaceTextures.Length; f++)
            {
                if (string.IsNullOrEmpty(m.FaceTextures[f]))
                    untexturedFaces++;
                else
                {
                    texturedFaces++;
                    if (!missing.Contains(m.FaceTextures[f], StringComparer.OrdinalIgnoreCase))
                        resolvedFaces++;
                }
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{name} · {ResourceService.HumanSize(size)}");
        sb.AppendLine($"{meshes.Count} mesh(es) · {totalVerts:N0} verts · {totalTris:N0} triangles");
        sb.AppendLine($"faces: {resolvedFaces:N0} textured · {texturedFaces - resolvedFaces:N0} unresolved · {untexturedFaces:N0} no texture");
        sb.AppendLine($"textures: {loadedTextures}/{textureNames} loaded");
        if (missing.Count > 0)
        {
            sb.Append("missing: ");
            sb.Append(string.Join(", ", missing.Take(8)));
            if (missing.Count > 8) sb.Append($" +{missing.Count - 8} more");
        }
        sb.AppendLine();
        sb.AppendLine();
        sb.Append("Drag to orbit · scroll to zoom");
        return sb.ToString();
    }

    private void SetPreview(string? text = null, Bitmap? image = null, string? info = null,
        IReadOnlyList<S4League.Scn.ScnMesh>? meshes = null, IReadOnlyDictionary<string, ScnTexture>? textures = null)
    {
        PreviewText = text;
        PreviewImage = image;
        PreviewInfo = info;
        PreviewMeshes = meshes;
        PreviewTextures = textures;
        ShowText = text is not null;
        ShowImage = image is not null;
        ShowInfo = info is not null;
        ShowScene = meshes is not null && meshes.Count > 0;
        ShowTexturePicker = ShowScene && (textures is not null && textures.Count > 0);
        // The upscale control is only meaningful when we have raw source pixels (DDS/TGA).
        ShowScalePicker = ShowImage && _previewSource is not null;
        // Note: bitmap lifetime is managed by DisposeImageAssets()/RegenerateImage(), not here.
    }

    private void ClearPreview()
    {
        DisposeImageAssets();
        SetPreview();
    }

    /// <summary>Releases the raw source and the displayed bitmaps for the current image preview.</summary>
    private void DisposeImageAssets()
    {
        _previewSource = null;
        _previewOriginalImage?.Dispose();
        _previewOriginalImage = null;
        _previewTempImage?.Dispose();
        _previewTempImage = null;
        _previewUpscaledBgra = null;
        CanExportUpscaled = false;
        PreviewImage = null;
    }

    /// <summary>
    /// Rebuilds the displayed image for the chosen scale. 1x reuses the cached original bitmap;
    /// 4x/8x run the in-process upscaler on the raw source pixels (never writing back to the
    /// archive — this is preview-only). Runs off the UI thread; a token discards stale results
    /// if the user changes the scale again mid-upscale.
    /// </summary>
    private async void RegenerateImage(int scale)
    {
        if (_previewSource is null) return;
        var token = ++_upscaleToken;
        var src = _previewSource;
        int w = _previewSourceW, h = _previewSourceH;

        long tw = (long)w * scale, th = (long)h * scale;
        if (tw * th > 96_000_000L)
        {
            Status = $"Upscale to {scale}x too large ({tw} x {th} pixels).";
            return;
        }

        Bitmap? result;
        UpscaledTexture? upRaw = null;
        bool usedAi = false;
        if (scale == 1)
        {
            result = _previewOriginalImage;
        }
        else
        {
            IsBusy = true;
            try
            {
                // Real-ESRGAN can hang the GPU on tiny inputs (e.g. a 4x4 fixture), so only
                // use it for reasonably-sized textures; small ones use the software upscaler.
                var esrgan = (w >= 64 && h >= 64)
                    ? AiTextureUpscaler.FindExecutable(_settings)
                    : null;
                if (esrgan is not null)
                {
                    // Remember the discovered path so later runs don't re-scan.
                    if (!string.Equals(_settings.RealesrganPath, esrgan, StringComparison.OrdinalIgnoreCase))
                    {
                        _settings.RealesrganPath = esrgan;
                        _settings.Save();
                    }
                    Status = $"Upscaling {scale}x (Real-ESRGAN)...";
                    var up = await Task.Run(() => AiTextureUpscaler.Upscale(src, w, h, scale, esrgan));
                    upRaw = up;
                    result = ImageLoader.FromBgra(up.Bgra, up.Width, up.Height);
                    usedAi = true;
                }
                else
                {
                    Status = $"Upscaling {scale}x (software)...";
                    var up = await Task.Run(() => TextureUpscaler.Upscale(src, w, h, scale));
                    upRaw = up;
                    result = ImageLoader.FromBgra(up.Bgra, up.Width, up.Height);
                }
            }
            catch (Exception ex)
            {
                Status = $"Upscale failed: {ex.Message}";
                return;
            }
            finally
            {
                if (token == _upscaleToken) IsBusy = false;
            }
        }

        if (token != _upscaleToken)
        {
            // A newer scale request won; the original stays cached, so only the temp is disposable.
            if (!ReferenceEquals(result, _previewOriginalImage)) result?.Dispose();
            return;
        }

        var old = PreviewImage;
        PreviewImage = result;
        _previewTempImage = ReferenceEquals(result, _previewOriginalImage) ? null : result;
        if (old is not null && !ReferenceEquals(old, _previewOriginalImage) && !ReferenceEquals(old, result))
            old.Dispose();
        if (token == _upscaleToken)
        {
            // Expose the upscaled raw BGRA for export (cleared when previewing at 1x).
            if (upRaw is not null)
            {
                _previewUpscaledBgra = upRaw.Value.Bgra;
                _previewUpscaledW = upRaw.Value.Width;
                _previewUpscaledH = upRaw.Value.Height;
            }
            else
            {
                _previewUpscaledBgra = null;
            }
            CanExportUpscaled = _previewUpscaledBgra is not null;
            Status = usedAi
                ? $"Previewing at {scale}x via Real-ESRGAN (upscale only, not saved)."
                : $"Previewing at {scale}x (upscale only, not saved).";
        }
    }

    [RelayCommand]
    private async Task ExportUpscaledAsync()
    {
        if (Ui is null || _previewUpscaledBgra is null) return;

        var scale = PreviewScale switch { "4x" => 4, "8x" => 8, _ => 1 };
        var baseName = Path.GetFileNameWithoutExtension(SelectedFile?.Entry.Name ?? "texture");
        var dest = await Ui.PickSaveImageAsync($"{baseName}_upscaled_{scale}x");
        if (string.IsNullOrWhiteSpace(dest)) return;

        try
        {
            if (Path.GetExtension(dest).Equals(".dds", StringComparison.OrdinalIgnoreCase))
            {
                var texconv = FindTexconv();
                if (texconv is null)
                {
                    await Ui.ShowMessageAsync("Export DDS",
                        "texconv.exe was not found, so DDS export is unavailable.\n\n" +
                        "Choose a .png name instead, or install texconv and set TexconvPath in settings.json.");
                    return;
                }
                await Task.Run(() => ExportDds(texconv, dest,
                    _previewUpscaledBgra!, _previewUpscaledW, _previewUpscaledH));
            }
            else
            {
                var png = PngCodec.EncodeBgra(_previewUpscaledBgra, _previewUpscaledW, _previewUpscaledH);
                await File.WriteAllBytesAsync(dest, png);
            }
            Status = $"Exported upscaled texture to {dest}.";
        }
        catch (Exception ex)
        {
            await Ui.ShowMessageAsync("Export failed", ex.Message);
        }
    }

    /// <summary>Locates texconv.exe: configured path, then a couple of well-known install spots.</summary>
    private string? FindTexconv()
    {
        if (!string.IsNullOrWhiteSpace(_settings.TexconvPath) && File.Exists(_settings.TexconvPath))
            return _settings.TexconvPath;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        {
            Path.Combine(home, "Downloads", "Texture Upscaler", "texconv.exe"),
            Path.Combine(AppContext.BaseDirectory, "texconv.exe"),
        };
        foreach (var c in candidates)
            if (File.Exists(c))
                return c;
        return null;
    }

    /// <summary>Converts the raw BGRA to a BC7 sRGB DDS via texconv, written directly to <paramref name="dest"/>.</summary>
    private static void ExportDds(string texconv, string dest, byte[] bgra, int w, int h)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "S4LResourceTool", "texconv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var png = Path.Combine(tempDir, "upscaled.png");
            File.WriteAllBytes(png, PngCodec.EncodeBgra(bgra, w, h));

            var outDir = Path.GetDirectoryName(dest)!;
            Directory.CreateDirectory(outDir);

            var psi = new ProcessStartInfo(texconv)
            {
                Arguments = $"-f BC7_UNORM_SRGB -nologo -y -o \"{outDir}\" \"{png}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Could not start texconv.");
            proc.StandardOutput.ReadToEnd();
            proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"texconv failed (exit code {proc.ExitCode}).");

            var produced = Path.Combine(outDir, "upscaled.dds");
            if (!File.Exists(produced))
                throw new InvalidOperationException("texconv produced no output file.");
            if (!string.Equals(Path.GetFullPath(produced), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                File.Move(produced, dest, overwrite: true);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* best effort */ }
        }
    }

    private void EnsureWatchTimer()
    {
        if (_watchTimer is not null) return;
        _watchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _watchTimer.Tick += async (_, _) => await CheckTrackedFilesAsync();
        _watchTimer.Start();
    }

    private async Task CheckTrackedFilesAsync()
    {
        if (Ui is null || _tracked.Count == 0) return;

        foreach (var (path, info) in _tracked.ToList())
        {
            DateTime last;
            try { last = File.GetLastWriteTime(path); } catch { continue; }
            if (last == info.LastWrite) continue;

            _tracked[path] = (info.Entry, last);
            var apply = await Ui.ConfirmAsync(info.Entry.FullName,
                "The file changed on disk. Apply your changes to the archive?");
            if (!apply) continue;

            try
            {
                _resources.Replace(info.Entry, await File.ReadAllBytesAsync(path));
                var row = Files.FirstOrDefault(r => r.Entry == info.Entry);
                if (row is not null) { row.IsModified = true; row.RefreshModified(); }
                if (SelectedFile?.Entry == info.Entry) await LoadPreviewAsync(SelectedFile);
                Status = $"Applied external changes to {info.Entry.Name} (remember to Save).";
            }
            catch (Exception ex) { Status = $"Re-import failed: {ex.Message}"; }
        }
    }
}
