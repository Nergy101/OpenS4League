using System.Numerics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using S4League.Resource;
using S4League.Scn;
using S4League.View;

namespace S4LCharacterViewer.Views;

public partial class MainWindow : Window
{
    private SceneContainer? _container;
    private S4Zip? _zip;
    private string? _sceneDir;
    private List<ScnMesh> _baseMeshes = new();
    private Dictionary<string, ScnTexture>? _textures;
    private DispatcherTimer? _timer;
    private double _animTime;
    private ModelAnimation? _selectedAnim;
    private bool _sliderBusy;

    public MainWindow()
    {
        InitializeComponent();
    }

    // ---- Open --------------------------------------------------------------

    private async void OpenScn_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open S4 scene",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("S4 scene") { Patterns = new[] { "*.scn" } } }
        });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path is null) return;

        try
        {
            _zip = null;
            _sceneDir = Path.GetDirectoryName(path);
            _container = SceneContainer.ReadFrom(path);
            LoadScene();
            InfoText.Text = $"Loaded {path}";
            Title = $"S4 League Character Viewer — {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            InfoText.Text = $"Failed to open: {ex.Message}";
        }
    }

    private async void OpenArchive_Click(object? sender, RoutedEventArgs e)
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
            _sceneDir = null;
            var scns = _zip.Values
                .Where(x => x.Name.EndsWith(".scn", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.FullName)
                .OrderBy(x => x)
                .ToList();
            ArchiveSceneCombo.ItemsSource = scns;
            ArchiveSceneCombo.IsEnabled = scns.Count > 0;
            if (scns.Count > 0)
            {
                ArchiveSceneCombo.SelectedIndex = 0;
                InfoText.Text = $"Archive opened ({_zip.Count} entries, {scns.Count} scenes).";
            }
            else
            {
                InfoText.Text = "Archive opened, but no .scn scenes found.";
            }
        }
        catch (Exception ex)
        {
            InfoText.Text = $"Failed to open archive: {ex.Message}";
        }
    }

    private void ArchiveSceneCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_zip is null || ArchiveSceneCombo.SelectedItem is not string fullName) return;
        try
        {
            var entry = _zip.Values.FirstOrDefault(x =>
                string.Equals(x.FullName, fullName, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return;
            _container = SceneContainer.ReadFrom(entry.GetData());
            LoadScene();
            InfoText.Text = $"Loaded {fullName}";
        }
        catch (Exception ex)
        {
            InfoText.Text = $"Failed to load scene: {ex.Message}";
        }
    }

    // ---- Scene loading -----------------------------------------------------

    private void LoadScene()
    {
        if (_container is null) return;
        _baseMeshes = SceneMeshBuilder.Build(_container);
        _textures = LoadTextures(_baseMeshes);
        _selectedAnim = null;
        _animTime = 0;

        ModelList.ItemsSource = _container.Models
            .Select(m => $"{m.Name}  ({m.Mesh.Vertices.Count} verts, {(m.WeightBone.Count > 0 ? "skinned" : "static")})")
            .ToList();

        AnimList.ItemsSource = _container.Models
            .SelectMany(m => m.Animation)
            .Select(a => $"{a.Name}  ({a.TransformKeyData2.Duration.TotalSeconds:0.###}s)")
            .ToList();

        var boneSys = _container.BoneSystems.Count;
        var bones = _container.Bones.Count;
        BoneInfo.Text = $"{bones} bones, {boneSys} bone systems, {_container.Boxes.Count} boxes, {_container.Shapes.Count} shapes.";

        // Texture override combo: "—" = auto (use per-face textures).
        var texNames = SceneMeshBuilder.DistinctTextureNames(_baseMeshes);
        var comboItems = new List<string> { "—" };
        comboItems.AddRange(texNames);
        TextureCombo.ItemsSource = comboItems;
        TextureCombo.SelectedIndex = 0;
        TextureCombo.IsEnabled = texNames.Count > 0;

        ApplyPreview();

        // Animation controls.
        bool hasAnim = _container.Models.Any(m => m.Animation.Count > 0);
        PlayBtn.IsEnabled = hasAnim;
        StopBtn.IsEnabled = hasAnim;
        TimeSlider.Maximum = 1;
        TimeSlider.Value = 0;
        if (hasAnim)
        {
            double maxDur = _container.Models.SelectMany(m => m.Animation).Max(a => a.TransformKeyData2.Duration.TotalSeconds);
            TimeSlider.Maximum = Math.Max(maxDur, 0.001);
        }
    }

    private Dictionary<string, ScnTexture> LoadTextures(List<ScnMesh> meshes)
    {
        var result = new Dictionary<string, ScnTexture>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in SceneMeshBuilder.DistinctTextureNames(meshes))
        {
            var tex = TryLoadTexture(name);
            if (tex is not null) result[name] = tex;
        }
        return result;
    }

    private ScnTexture? TryLoadTexture(string name)
    {
        if (_zip is not null)
        {
            var t = ScnTextureLoader.TryLoad(_zip, name);
            if (t is not null) return t;
        }
        if (_sceneDir is not null)
        {
            try
            {
                var baseName = name.Replace('\\', '/');
                var slash = baseName.LastIndexOf('/');
                if (slash >= 0) baseName = baseName[(slash + 1)..];
                var p = Path.Combine(_sceneDir, baseName);
                if (File.Exists(p)) return ScnTextureLoader.Decode(File.ReadAllBytes(p), baseName);
            }
            catch { /* ignore */ }
        }
        return null;
    }

    private void ApplyPreview()
    {
        Preview.Meshes = _baseMeshes;
        Preview.Textures = _textures;
    }

    private void TextureCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TextureCombo.SelectedItem is string s)
            Preview.TextureOverride = s == "—" ? null : s;
    }

    // ---- Animation playback -------------------------------------------------

    private void Play_Click(object? sender, RoutedEventArgs e)
    {
        if (AnimList.SelectedItem is not string sel) return;
        var anim = FindAnimation(sel);
        if (anim is null) return;
        _selectedAnim = anim;
        _animTime = 0;
        _timer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += Tick;
        _timer.Start();
        StopBtn.IsEnabled = true;
        PlayBtn.Content = "Playing…";
    }

    private void Stop_Click(object? sender, RoutedEventArgs e)
    {
        if (_timer is not null) _timer.Stop();
        _selectedAnim = null;
        _animTime = 0;
        _sliderBusy = true;
        TimeSlider.Value = 0;
        _sliderBusy = false;
        ApplyPreview();
        PlayBtn.Content = "Play";
        StopBtn.IsEnabled = false;
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (_selectedAnim is null) return;
        double dur = _selectedAnim.TransformKeyData2.Duration.TotalMilliseconds;
        if (dur <= 0) dur = 1000;
        _animTime += 33;
        if (_animTime > dur) _animTime = 0;
        ApplyAnimationFrame();
    }

    private void TimeSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_sliderBusy || _selectedAnim is null) return;
        _animTime = TimeSlider.Value * 1000.0;
        ApplyAnimationFrame();
    }

    private void ApplyAnimationFrame()
    {
        if (_selectedAnim is null) return;
        var delta = AccumulateDelta(_selectedAnim.TransformKeyData2, _animTime);
        Preview.Meshes = OffsetMeshes(_baseMeshes, delta);
        Preview.Textures = _textures;
        TimeText.Text = $"{_animTime / 1000.0:0.###}s / {_selectedAnim.TransformKeyData2.Duration.TotalSeconds:0.###}s";
    }

    private static Vector3 AccumulateDelta(TransformKeyData2 data, double ms)
    {
        if (data.TransformKey is not { } tk) return Vector3.Zero;
        var delta = Vector3.Zero;
        double remaining = ms;
        foreach (var k in tk.TKey)
        {
            double d = k.Duration.TotalMilliseconds;
            if (remaining >= d) { delta += k.Translation; remaining -= d; }
            else { delta += k.Translation * (float)(d > 0 ? remaining / d : 1); break; }
        }
        return delta;
    }

    private static List<ScnMesh> OffsetMeshes(IReadOnlyList<ScnMesh> source, Vector3 offset)
    {
        var result = new List<ScnMesh>(source.Count);
        foreach (var m in source)
        {
            var verts = new Vector3[m.Vertices.Length];
            for (int i = 0; i < verts.Length; i++) verts[i] = m.Vertices[i] + offset;
            result.Add(new ScnMesh
            {
                Name = m.Name,
                Vertices = verts,
                Indices = m.Indices,
                Normals = m.Normals,
                Uv = m.Uv,
                Skinned = m.Skinned,
                FaceTextures = m.FaceTextures
            });
        }
        return result;
    }

    private ModelAnimation? FindAnimation(string label)
    {
        if (_container is null) return null;
        var name = label;
        var sp = label.IndexOf("  (", StringComparison.Ordinal);
        if (sp >= 0) name = label[..sp];
        foreach (var m in _container.Models)
            foreach (var a in m.Animation)
                if (a.Name == name) return a;
        return null;
    }
}
