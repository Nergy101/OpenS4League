using System.Numerics;

namespace S4League.Scn;

/// <summary>Base type for every block stored in a .scn scene.</summary>
public abstract class SceneChunk : IScnSerializable
{
    public SceneContainer Container { get; }
    public abstract ChunkType ChunkType { get; }

    public string Name { get; set; } = "";
    public string SubName { get; set; } = "";

    public Version Version { get; set; } = Version.Two;
    public Matrix4x4 Matrix { get; set; } = Matrix4x4.Identity;
    public Version Version2 { get; set; } = Version.Two;

    protected SceneChunk(SceneContainer container)
    {
        Container = container;
    }

    public virtual void Serialize(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write((int)Version);
        w.Write(Matrix);
        w.Write((int)Version2);
    }

    public virtual void Deserialize(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Version = (Version)r.ReadInt32();
        Matrix = r.ReadMatrix();
        Version2 = (Version)r.ReadInt32();
    }

    public override string ToString() => $"{Name} - {SubName}";
}
