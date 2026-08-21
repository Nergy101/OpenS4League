using System.Numerics;

namespace S4League.Scn;

public class ModelAnimation
{
    public string Name = "";
    public TransformKeyData2 TransformKeyData2 = new();
}

public class TransformKeyData2 : TransformKeyData
{
    public IList<MorphKey> MorphKeys { get; set; } = new List<MorphKey>();

    public override void Deserialize(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        base.Deserialize(stream);
        MorphKeys = r.DeserializeArray<MorphKey>(r.ReadInt32());
    }

    public override void Serialize(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        base.Serialize(stream);
        w.Write(MorphKeys.Count);
        w.Serialize(MorphKeys);
    }
}

public class BoneAnimation
{
    public string Name = "";
    public string? Copy;
    public TransformKeyData? TransformKeyData;
}

public class TransformKeyData : IScnSerializable
{
    public TimeSpan Duration { get; set; }
    public TransformKey? TransformKey { get; set; }
    public IList<FloatKey> FloatKeys { get; set; } = new List<FloatKey>();

    public virtual void Deserialize(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Duration = TimeSpan.FromMilliseconds(r.ReadUInt32());

        bool flag = r.ReadBoolean();
        if (flag)
            TransformKey = r.Deserialize<TransformKey>();

        FloatKeys = r.DeserializeArray<FloatKey>(r.ReadInt32());
    }

    public virtual void Serialize(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write((uint)Duration.TotalMilliseconds);

        w.Write(TransformKey != null);
        if (TransformKey != null)
            w.Serialize(TransformKey);

        w.Write(FloatKeys.Count);
        w.Serialize(FloatKeys);
    }
}

public class TransformKey : IScnSerializable
{
    public Vector3 Translation = Vector3.Zero;
    public Quaternion Rotation = Quaternion.Identity;
    public Vector3 Scale = Vector3.Zero;

    public IList<TKey> TKey { get; set; } = new List<TKey>();
    public IList<RKey> RKey { get; set; } = new List<RKey>();
    public IList<SKey> SKey { get; set; } = new List<SKey>();

    public void Deserialize(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Translation = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        Rotation = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        Scale = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        uint count = r.ReadUInt32();
        for (int n = 0; n < count; n++)
            TKey.Add(new TKey { Duration = TimeSpan.FromMilliseconds(r.ReadUInt32()), Translation = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) });

        count = r.ReadUInt32();
        for (int n = 0; n < count; n++)
            RKey.Add(new RKey { Duration = TimeSpan.FromMilliseconds(r.ReadUInt32()), Rotation = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) });

        count = r.ReadUInt32();
        for (int n = 0; n < count; n++)
            SKey.Add(new SKey { Duration = TimeSpan.FromMilliseconds(r.ReadUInt32()), Scale = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) });
    }

    public void Serialize(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write(Translation.X); w.Write(Translation.Y); w.Write(Translation.Z);
        w.Write(Rotation.X); w.Write(Rotation.Y); w.Write(Rotation.Z); w.Write(Rotation.W);
        w.Write(Scale.X); w.Write(Scale.Y); w.Write(Scale.Z);

        w.Write(TKey.Count);
        foreach (var t in TKey)
        {
            w.Write((uint)t.Duration.TotalMilliseconds);
            w.Write(t.Translation.X); w.Write(t.Translation.Y); w.Write(t.Translation.Z);
        }

        w.Write(RKey.Count);
        foreach (var r in RKey)
        {
            w.Write((uint)r.Duration.TotalMilliseconds);
            w.Write(r.Rotation.X); w.Write(r.Rotation.Y); w.Write(r.Rotation.Z); w.Write(r.Rotation.W);
        }

        w.Write(SKey.Count);
        foreach (var s in SKey)
        {
            w.Write((uint)s.Duration.TotalMilliseconds);
            w.Write(s.Scale.X); w.Write(s.Scale.Y); w.Write(s.Scale.Z);
        }
    }
}

public struct TKey
{
    public TimeSpan Duration;
    public Vector3 Translation;
}

public struct RKey
{
    public TimeSpan Duration;
    public Quaternion Rotation;
}

public struct SKey
{
    public TimeSpan Duration;
    public Vector3 Scale;
}

public struct VKey
{
    public TimeSpan Duration;
    public float Alpha;
}

public class FloatKey : IScnSerializable
{
    public TimeSpan Duration { get; set; }
    public float Alpha { get; set; }

    public void Deserialize(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Duration = TimeSpan.FromMilliseconds(r.ReadUInt32());
        Alpha = r.ReadSingle();
    }

    public void Serialize(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write((uint)Duration.TotalMilliseconds);
        w.Write(Alpha);
    }
}

public class MorphKey : IScnSerializable
{
    public TimeSpan Duration { get; set; }
    public IList<Quaternion> Rotations { get; set; } = new List<Quaternion>();
    public IList<Vector3> Positions { get; set; } = new List<Vector3>();

    public void Deserialize(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Duration = TimeSpan.FromMilliseconds(r.ReadUInt32());

        int count = r.ReadInt32();
        for (int j = 0; j < count; j++)
            Rotations.Add(new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));

        count = r.ReadInt32();
        for (int j = 0; j < count; j++)
            Positions.Add(new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
    }

    public void Serialize(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write((uint)Duration.TotalMilliseconds);

        w.Write(Rotations.Count);
        foreach (var r in Rotations)
        {
            w.Write(r.X); w.Write(r.Y); w.Write(r.Z); w.Write(r.W);
        }

        w.Write(Positions.Count);
        foreach (var p in Positions)
        {
            w.Write(p.X); w.Write(p.Y); w.Write(p.Z);
        }
    }
}
