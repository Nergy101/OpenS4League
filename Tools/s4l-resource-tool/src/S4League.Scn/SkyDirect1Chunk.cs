using System.Numerics;

namespace S4League.Scn;

/// <summary>A SkyDirect1 chunk: ambient/lighting color ramps. No geometry, shown as info.</summary>
public class SkyDirect1Chunk : SceneChunk
{
    public override ChunkType ChunkType => ChunkType.SkyDirect1;

    public Color Color1 = Color.White;
    public Color Color2 = Color.White;
    public Color Color3 = Color.White;
    public Color Color4 = Color.White;
    public Color Color5 = Color.White;
    public Color Color6 = Color.White;
    public Color Color7 = Color.White;
    public short Short1;

    public SkyDirect1Chunk(SceneContainer container)
        : base(container) { }

    public override void Serialize(Stream stream)
    {
        base.Serialize(stream);

        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write(Color1); w.Write(Color2); w.Write(Color3); w.Write(Color4);
        w.Write(Color5); w.Write(Color6); w.Write(Color7);
        w.Write(Short1);
    }

    public override void Deserialize(Stream stream)
    {
        base.Deserialize(stream);

        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Color1 = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        Color2 = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        Color3 = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        Color4 = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        Color5 = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        Color6 = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        Color7 = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        r.ReadInt32();
        r.ReadInt32();
    }
}

/// <summary>RGBA color tuple (replaces Unity's Color).</summary>
public readonly struct Color
{
    public readonly float R, G, B, A;
    public Color(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
    public static readonly Color White = new(1f, 1f, 1f, 1f);
    public override string ToString() => $"({R:F3}, {G:F3}, {B:F3}, {A:F3})";
}
