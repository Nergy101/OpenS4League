using System.Globalization;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using S4League.Resource;
using S4League.Scn;
using S4League.View;

namespace S4LMapEditor.Views;

public partial class MainWindow : Window
{
    private SceneContainer? _container;
    private S4Zip? _zip;
    private string? _sceneDir;
    private readonly List<string> _labels = new();
    private readonly List<SceneChunk?> _refs = new();
    private Dictionary<string, ScnTexture>? _textures;

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
            AfterLoad($"{Path.GetFileName(path)} loaded");
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
            if (scns.Count == 0) { InfoText.Text = "No .scn scenes found in archive."; return; }
            var entry = scns.OrderBy(x => x.FullName).First();
            _container = SceneContainer.ReadFrom(entry.GetData());
            AfterLoad($"Loaded '{entry.FullName}' (from {Path.GetFileName(path)}, {scns.Count} scenes)");
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
        RebuildTree();
        RefreshPreview();
        InfoText.Text = msg;
        Title = $"S4 League Map Editor — {_container!.Header.Name}";
    }

    // ---- Tree --------------------------------------------------------------

    private void RebuildTree()
    {
        _labels.Clear();
        _refs.Clear();
        if (_container is null) { ChunkList.ItemsSource = null; CountText.Text = ""; return; }

        void Group(string name, IEnumerable<SceneChunk> chunks)
        {
            _labels.Add(name);
            _refs.Add(null);
            foreach (var c in chunks)
            {
                _labels.Add($"   {c.Name}  <{c.SubName}>");
                _refs.Add(c);
            }
        }

        Group("[Models]", _container.Models);
        Group("[Boxes]", _container.Boxes);
        Group("[Bones]", _container.Bones);
        Group("[BoneSystems]", _container.BoneSystems);
        Group("[Shapes]", _container.Shapes);

        ChunkList.ItemsSource = _labels.ToList();
        CountText.Text = $"{_container.Models.Count} models · {_container.Boxes.Count} boxes · " +
                         $"{_container.Bones.Count} bones · {_container.BoneSystems.Count} bone systems · {_container.Shapes.Count} shapes";
    }

    private void ChunkList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        int idx = ChunkList.SelectedIndex;
        if (idx < 0 || idx >= _refs.Count) { ApplyTransform.IsEnabled = false; return; }
        var c = _refs[idx];
        if (c is null) { ApplyTransform.IsEnabled = false; SelName.Text = ""; return; }

        Matrix4x4.Decompose(c.Matrix, out var s, out var r, out var t);
        Tx.Text = F(t.X); Ty.Text = F(t.Y); Tz.Text = F(t.Z);
        Rx.Text = F(r.X); Ry.Text = F(r.Y); Rz.Text = F(r.Z); Rw.Text = F(r.W);
        Sx.Text = F(s.X); Sy.Text = F(s.Y); Sz.Text = F(s.Z);
        SelName.Text = $"{c.Name}  ({c.ChunkType})";
        DetailText.Text = Describe(c);
        ApplyTransform.IsEnabled = true;
    }

    private void ApplyTransform_Click(object? sender, RoutedEventArgs e)
    {
        int idx = ChunkList.SelectedIndex;
        if (idx < 0 || idx >= _refs.Count) return;
        var c = _refs[idx];
        if (c is null) return;

        var t = new Vector3(P(Tx.Text), P(Ty.Text), P(Tz.Text));
        var r = new Quaternion(P(Rx.Text), P(Ry.Text), P(Rz.Text), P(Rw.Text));
        if (r.LengthSquared() < 1e-8f) r = Quaternion.Identity; else r = Quaternion.Normalize(r);
        var s = new Vector3(P(Sx.Text), P(Sy.Text), P(Sz.Text));
        c.Matrix = Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);
        RebuildTree();
        RefreshPreview();
        InfoText.Text = $"Updated '{c.Name}' transform.";
    }

    private static string Describe(SceneChunk c) => c switch
    {
        ModelChunk m => $"Mesh: {m.Mesh.Vertices.Count} verts, {m.Mesh.Faces.Count} faces, {(m.WeightBone.Count > 0 ? $"{m.WeightBone.Count} bones" : "static")}, {m.Animation.Count} anims",
        BoxChunk b => $"Box: size {b.Size}",
        ShapeChunk s => $"Shape: {s.Unk.Count} segments",
        BoneChunk => "Bone",
        _ => c.ChunkType.ToString()
    };

    // ---- Edit operations ---------------------------------------------------

    private void AddBox_Click(object? sender, RoutedEventArgs e)
    {
        if (_container is null) return;
        var box = new BoxChunk(_container)
        {
            Name = UniqueName("Box"),
            SubName = _container.Header.Name,
            Matrix = Matrix4x4.CreateTranslation(new Vector3(2, 2, 0))
        };
        _container.Add(box);
        AfterEdit($"Added box '{box.Name}'");
    }

    private void AddShape_Click(object? sender, RoutedEventArgs e)
    {
        if (_container is null) return;
        var shp = new ShapeChunk(_container)
        {
            Name = UniqueName("Shape"),
            SubName = _container.Header.Name,
            Matrix = Matrix4x4.Identity
        };
        shp.Unk.Add((new Vector3(-1, 0, 0), new Vector3(1, 0, 0)));
        shp.Unk.Add((new Vector3(0, -1, 0), new Vector3(0, 1, 0)));
        _container.Add(shp);
        AfterEdit($"Added shape '{shp.Name}'");
    }

    private void Duplicate_Click(object? sender, RoutedEventArgs e)
    {
        if (_container is null) return;
        int idx = ChunkList.SelectedIndex;
        if (idx < 0 || idx >= _refs.Count) return;
        if (_refs[idx] is not ModelChunk m) { InfoText.Text = "Select a model to duplicate."; return; }

        var clone = CloneModel(m);
        _container.Add(clone);
        AfterEdit($"Duplicated '{m.Name}' -> '{clone.Name}'");
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (_container is null) return;
        int idx = ChunkList.SelectedIndex;
        if (idx < 0 || idx >= _refs.Count) return;
        var c = _refs[idx];
        if (c is null) return;

        _container.Remove(c);
        switch (c)
        {
            case ModelChunk m: _container.Models.Remove(m); break;
            case BoxChunk b: _container.Boxes.Remove(b); break;
            case BoneChunk bn: _container.Bones.Remove(bn); break;
            case BoneSystemChunk bs: _container.BoneSystems.Remove(bs); break;
            case ShapeChunk sh: _container.Shapes.Remove(sh); break;
        }
        AfterEdit($"Deleted '{c.Name}'");
    }

    private void AfterEdit(string msg)
    {
        RebuildTree();
        RefreshPreview();
        InfoText.Text = msg;
    }

    private ModelChunk CloneModel(ModelChunk src)
    {
        var m = new ModelChunk(_container!);
        using var ms = new MemoryStream();
        src.Serialize(ms);
        ms.Position = 0;
        m.Deserialize(ms);
        m.Name = UniqueName(src.Name + "_copy");
        return m;
    }

    private string UniqueName(string baseName)
    {
        var existing = new HashSet<string>(_container!.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(baseName)) return baseName;
        for (int i = 1; ; i++)
        {
            var candidate = $"{baseName}_{i}";
            if (!existing.Contains(candidate)) return candidate;
        }
    }

    // ---- Preview -----------------------------------------------------------

    private void RefreshPreview()
    {
        if (_container is null) { Preview.Meshes = null; return; }
        var meshes = SceneMeshBuilder.Build(_container);
        _textures = LoadTextures(meshes);
        Preview.Meshes = meshes;
        Preview.Textures = _textures;
        StatusText.Text = $"{meshes.Count} renderable meshes · {(_textures?.Count ?? 0)} textures";
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

    // ---- Helpers -----------------------------------------------------------

    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static float P(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
    }
}
