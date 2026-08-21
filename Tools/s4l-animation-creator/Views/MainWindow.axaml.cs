using System.Globalization;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using S4League.Resource;
using S4League.Scn;
using S4League.View;

namespace S4LAnimationCreator.Views;

public partial class MainWindow : Window
{
    private SceneContainer? _container;
    private S4Zip? _zip;
    private string? _sceneDir;
    private ModelChunk? _selectedModel;
    private ModelAnimation? _selectedAnim;
    private List<ScnMesh> _baseMeshes = new();
    private Dictionary<string, ScnTexture>? _textures;
    private DispatcherTimer? _timer;
    private double _animTime;
    private int _selectedKeyIndex = -1;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
    }

    // ---- Open / Save -------------------------------------------------------

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
            AfterLoad($"Loaded {Path.GetFileName(path)}");
        }
        catch (Exception ex) { InfoText.Text = $"Open failed: {ex.Message}"; }
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
            var scns = _zip.Values.Where(x => x.Name.EndsWith(".scn", StringComparison.OrdinalIgnoreCase)).ToList();
            if (scns.Count == 0) { InfoText.Text = "No .scn scenes in archive."; return; }
            var entry = scns.OrderBy(x => x.FullName).First();
            _container = SceneContainer.ReadFrom(entry.GetData());
            AfterLoad($"Loaded '{entry.FullName}'");
        }
        catch (Exception ex) { InfoText.Text = $"Open failed: {ex.Message}"; }
    }

    private async void SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        if (_container is null) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save scene",
            DefaultExtension = "scn",
            FileTypeChoices = new[] { new FilePickerFileType("S4 scene") { Patterns = new[] { "*.scn" } } },
            SuggestedFileName = "scene.scn"
        });
        var path = file?.TryGetLocalPath();
        if (path is null) return;
        try
        {
            _container.Write(path);
            InfoText.Text = $"Saved to {path}";
        }
        catch (Exception ex) { InfoText.Text = $"Save failed: {ex.Message}"; }
    }

    private void AfterLoad(string msg)
    {
        Stop();
        _baseMeshes = SceneMeshBuilder.Build(_container!);
        _textures = LoadTextures(_baseMeshes);
        ModelCombo.ItemsSource = _container!.Models.Where(m => m.Animation.Count > 0).Select(m => m.Name).ToList();
        _selectedModel = null;
        _selectedAnim = null;
        AnimCombo.ItemsSource = null;
        KeyList.ItemsSource = null;
        DurationBox.Text = "";
        InfoText.Text = msg;
        Title = $"S4 League Animation Creator — {_container.Header.Name}";
        Preview.Meshes = _baseMeshes;
        Preview.Textures = _textures;
    }

    // ---- Selection ---------------------------------------------------------

    private void ModelCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_container is null || ModelCombo.SelectedItem is not string name) return;
        _selectedModel = _container.Models.FirstOrDefault(m => m.Name == name);
        if (_selectedModel is null) return;
        AnimCombo.ItemsSource = _selectedModel.Animation.Select(a => $"{a.Name}  ({a.TransformKeyData2.Duration.TotalSeconds:0.###}s)").ToList();
        _selectedAnim = null;
        if (AnimCombo.ItemsSource is IReadOnlyList<string> list && list.Count > 0) AnimCombo.SelectedIndex = 0;
    }

    private void AnimCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_selectedModel is null || AnimCombo.SelectedItem is not string label) return;
        var name = label.Split("  (")[0];
        _selectedAnim = _selectedModel.Animation.FirstOrDefault(a => a.Name == name);
        if (_selectedAnim is null) return;
        _selectedKeyIndex = -1;
        RefreshDuration();
        RefreshKeys();
        InfoText.Text = $"Editing animation '{name}'";
    }

    // ---- Duration & keyframes ---------------------------------------------

    private void RefreshDuration()
    {
        if (_selectedAnim is null) return;
        _busy = true;
        DurationBox.Text = _selectedAnim.TransformKeyData2.Duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        _busy = false;
    }

    private void RefreshKeys()
    {
        if (_selectedAnim is null) { KeyList.ItemsSource = null; return; }
        var tk = _selectedAnim.TransformKeyData2.TransformKey;
        var keys = tk?.TKey ?? new List<TKey>();
        KeyList.ItemsSource = keys.Select((k, i) =>
            $"[{i}] t={k.Duration.TotalMilliseconds:0}ms  ({k.Translation.X:0.###}, {k.Translation.Y:0.###}, {k.Translation.Z:0.###})").ToList();
    }

    private void DurationBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_busy || _selectedAnim is null) return;
        if (float.TryParse(DurationBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
            _selectedAnim.TransformKeyData2.Duration = TimeSpan.FromSeconds(seconds);
    }

    private void KeyList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedKeyIndex = KeyList.SelectedIndex;
        if (_selectedAnim is null || _selectedKeyIndex < 0) return;
        var tk = _selectedAnim.TransformKeyData2.TransformKey;
        if (tk is null || _selectedKeyIndex >= tk.TKey.Count) return;
        var k = tk.TKey[_selectedKeyIndex];
        KeyX.Text = F(k.Translation.X); KeyY.Text = F(k.Translation.Y); KeyZ.Text = F(k.Translation.Z);
    }

    private void ApplyKey_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedAnim is null || _selectedKeyIndex < 0) return;
        var tk = EnsureTransformKey();
        if (_selectedKeyIndex >= tk.TKey.Count) return;
        var k = tk.TKey[_selectedKeyIndex];
        k.Translation = new Vector3(P(KeyX.Text), P(KeyY.Text), P(KeyZ.Text));
        tk.TKey[_selectedKeyIndex] = k;
        RefreshKeys();
        InfoText.Text = $"Updated key {_selectedKeyIndex}.";
    }

    private void AddKey_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedAnim is null) return;
        var tk = EnsureTransformKey();
        double dur = tk.TKey.Count > 0 ? tk.TKey[^1].Duration.TotalMilliseconds + 100 : 0;
        tk.TKey.Add(new TKey { Duration = TimeSpan.FromMilliseconds(dur), Translation = Vector3.Zero });
        RefreshKeys();
        KeyList.SelectedIndex = tk.TKey.Count - 1;
    }

    private void DelKey_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedAnim is null || _selectedKeyIndex < 0) return;
        var tk = EnsureTransformKey();
        if (_selectedKeyIndex >= tk.TKey.Count) return;
        tk.TKey.RemoveAt(_selectedKeyIndex);
        _selectedKeyIndex = -1;
        RefreshKeys();
        InfoText.Text = "Removed key.";
    }

    private void ClearKeys_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedAnim is null) return;
        var tk = EnsureTransformKey();
        tk.TKey.Clear();
        _selectedKeyIndex = -1;
        RefreshKeys();
        InfoText.Text = "Cleared translation keys.";
    }

    private static TransformKey EnsureTransformKey(ModelAnimation anim)
    {
        return anim.TransformKeyData2.TransformKey ??= new TransformKey();
    }

    private TransformKey EnsureTransformKey()
    {
        if (_selectedAnim is null) throw new InvalidOperationException("no animation");
        return EnsureTransformKey(_selectedAnim);
    }

    // ---- Playback ----------------------------------------------------------

    private void Play_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedAnim is null) return;
        _animTime = 0;
        _timer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += Tick;
        _timer.Start();
        PlayBtn.Content = "Playing…";
    }

    private void Stop_Click(object? sender, RoutedEventArgs e) => Stop();

    private void Stop()
    {
        if (_timer is not null) _timer.Stop();
        _animTime = 0;
        _busy = true;
        TimeSlider.Value = 0;
        _busy = false;
        ApplyFrame();
        PlayBtn.Content = "Play";
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (_selectedAnim is null) return;
        double dur = _selectedAnim.TransformKeyData2.Duration.TotalMilliseconds;
        if (dur <= 0) dur = 1000;
        _animTime += 33;
        if (_animTime > dur) _animTime = 0;
        ApplyFrame();
    }

    private void TimeSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_busy || _selectedAnim is null) return;
        _animTime = TimeSlider.Value * 1000.0;
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (_selectedAnim is null) { TimeText.Text = ""; return; }
        var delta = AccumulateDelta(_selectedAnim.TransformKeyData2, _animTime);
        Preview.Meshes = OffsetMeshes(_baseMeshes, delta);
        Preview.Textures = _textures;
        double dur = _selectedAnim.TransformKeyData2.Duration.TotalSeconds;
        TimeText.Text = $"{_animTime / 1000.0:0.###}s / {dur:0.###}s";
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
                Name = m.Name, Vertices = verts, Indices = m.Indices,
                Normals = m.Normals, Uv = m.Uv, Skinned = m.Skinned, FaceTextures = m.FaceTextures
            });
        }
        return result;
    }

    // ---- Texture helpers ---------------------------------------------------

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

    // ---- Helpers -----------------------------------------------------------

    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static float P(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
    }
}
