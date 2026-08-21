using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using S4League.Scn;

namespace S4League.View;

/// <summary>
/// Interactive 3D preview for a parsed .scn scene: drag to orbit, scroll to zoom.
/// Renders via the headless <see cref="ScnRenderer"/> into a WriteableBitmap.
/// </summary>
public class ScnPreviewView : Control
{
    private WriteableBitmap? _bitmap;
    private byte[] _buffer = Array.Empty<byte>();
    private bool _dirty = true;
    private bool _dragging;
    private Point _last;
    private ScnCamera _camera;

    public static readonly StyledProperty<IReadOnlyList<ScnMesh>?> MeshesProperty =
        AvaloniaProperty.Register<ScnPreviewView, IReadOnlyList<ScnMesh>?>(nameof(Meshes));

    public static readonly StyledProperty<IReadOnlyDictionary<string, ScnTexture>?> TexturesProperty =
        AvaloniaProperty.Register<ScnPreviewView, IReadOnlyDictionary<string, ScnTexture>?>(nameof(Textures));

    public static readonly StyledProperty<string?> TextureOverrideProperty =
        AvaloniaProperty.Register<ScnPreviewView, string?>(nameof(TextureOverride));

    public IReadOnlyList<ScnMesh>? Meshes
    {
        get => GetValue(MeshesProperty);
        set => SetValue(MeshesProperty, value);
    }

    public IReadOnlyDictionary<string, ScnTexture>? Textures
    {
        get => GetValue(TexturesProperty);
        set => SetValue(TexturesProperty, value);
    }

    /// <summary>When non-null, forces every face to render with this texture name.</summary>
    public string? TextureOverride
    {
        get => GetValue(TextureOverrideProperty);
        set => SetValue(TextureOverrideProperty, value);
    }

    public ScnPreviewView()
    {
        ClipToBounds = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MeshesProperty)
        {
            FitCamera();
            _dirty = true;
            InvalidateVisual();
        }
        else if (change.Property == TexturesProperty || change.Property == TextureOverrideProperty)
        {
            _dirty = true;
            InvalidateVisual();
        }
    }

    private void FitCamera()
    {
        var meshes = Meshes;
        int w = Math.Max(1, (int)Bounds.Width);
        int h = Math.Max(1, (int)Bounds.Height);
        _camera = ScnRenderer.FitCamera(meshes ?? new List<ScnMesh>(), w, h);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Meshes is null || Meshes.Count == 0) return;
        _dragging = true;
        _last = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        var p = e.GetPosition(this);
        var dx = (float)(p.X - _last.X);
        var dy = (float)(p.Y - _last.Y);
        _last = p;
        _camera.Yaw += dx * 0.008f;
        _camera.Pitch = Math.Clamp(_camera.Pitch - dy * 0.008f, -1.55f, 1.55f);
        _dirty = true;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Meshes is null || Meshes.Count == 0) return;
        _camera.Distance *= MathF.Pow(1.1f, (float)-e.Delta.Y);
        _camera.Distance = Math.Clamp(_camera.Distance, 0.5f, 1e6f);
        _dirty = true;
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        FitCamera();
        _dirty = true;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        int w = Math.Max(1, (int)Bounds.Width);
        int h = Math.Max(1, (int)Bounds.Height);
        context.FillRectangle(new SolidColorBrush(Avalonia.Media.Color.FromRgb(24, 26, 32)), new Rect(0, 0, w, h));

        var meshes = Meshes;

        if (meshes is null || meshes.Count == 0)
        {
            var ft = new FormattedText(
                "Open a .scn file to preview.",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                13,
                new SolidColorBrush(Avalonia.Media.Color.FromRgb(150, 150, 160)));
            context.DrawText(ft, new Point(12, h / 2f - 10));
            return;
        }

        if (_bitmap is null || _bitmap.PixelSize.Width != w || _bitmap.PixelSize.Height != h)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            _buffer = new byte[w * h * 4];
            _dirty = true;
        }

        if (_dirty)
        {
            ScnRenderer.Render(meshes, _camera, w, h, _buffer, Textures, TextureOverride);
            using var fb = _bitmap.Lock();
            System.Runtime.InteropServices.Marshal.Copy(_buffer, 0, fb.Address, _buffer.Length);
            _dirty = false;
        }

        context.DrawImage(_bitmap, new Rect(0, 0, w, h));
    }
}
