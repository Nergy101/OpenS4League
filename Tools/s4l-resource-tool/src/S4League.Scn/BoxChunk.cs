using System.Numerics;

namespace S4League.Scn;

/// <summary>A Box chunk: fumbi, spawn points, deadzones. Mostly invisible, so the preview
/// treats it as an optional wireframe box.</summary>
public class BoxChunk : SceneChunk
{
    public override ChunkType ChunkType => ChunkType.Box;

    public float Unk { get; set; } = 0.10000000149011612f;
    public float Unk2 { get; set; }
    public int Unk3 { get; set; }
    public Vector3[] Unk4 { get; set; } = new[]
    {
        new Vector3(1, 0, 0),
        new Vector3(0, 1, 0),
        new Vector3(0, 0, 1)
    };
    public Vector3 Size { get; set; } = new(1, 1, 1);

    public BoxChunk(SceneContainer container)
        : base(container) { }

    public override void Serialize(Stream stream)
    {
        if (Unk4.Length != 3)
            throw new Exception("Unk4 must have a length of 3");

        base.Serialize(stream);

        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write((int)Version);
        w.Write(Unk);
        w.Write(Unk2);
        w.Write(Unk3);

        foreach (var vec in Unk4)
        {
            w.Write(vec.X); w.Write(vec.Y); w.Write(vec.Z);
        }

        w.Write(Size.X); w.Write(Size.Y); w.Write(Size.Z);
    }

    public override void Deserialize(Stream stream)
    {
        base.Deserialize(stream);

        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        r.ReadInt32();
        Unk = r.ReadInt32();
        Unk2 = r.ReadSingle();
        Unk3 = r.ReadInt32();

        for (int i = 0; i < 3; i++)
            Unk4[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        Size = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}
