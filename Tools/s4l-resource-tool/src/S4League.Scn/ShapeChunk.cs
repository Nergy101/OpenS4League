using System.Numerics;

namespace S4League.Scn;

/// <summary>A Shape chunk: a set of line/point pairs, rendered as wires in the preview.</summary>
public class ShapeChunk : SceneChunk
{
    public override ChunkType ChunkType => ChunkType.Shape;

    public IList<(Vector3 A, Vector3 B)> Unk { get; set; } = new List<(Vector3 A, Vector3 B)>();

    public ShapeChunk(SceneContainer container)
        : base(container) { }

    public override void Serialize(Stream stream)
    {
        base.Serialize(stream);

        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (Version2 == Version.One || Version2 == Version.Two)
        {
            w.Write(Unk.Count);
            foreach (var (a, b) in Unk)
            {
                w.Write(a.X); w.Write(a.Y); w.Write(a.Z);
                w.Write(b.X); w.Write(b.Y); w.Write(b.Z);
            }
        }
    }

    public override void Deserialize(Stream stream)
    {
        base.Deserialize(stream);

        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (Version2 == Version.One || Version2 == Version.Two)
        {
            uint count = r.ReadUInt32();
            for (int i = 0; i < count; i++)
            {
                var a = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var b = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                Unk.Add((a, b));
            }
        }
    }
}
