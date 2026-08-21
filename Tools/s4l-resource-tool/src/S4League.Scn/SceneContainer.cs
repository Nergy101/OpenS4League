using System.Collections.ObjectModel;
using System.Numerics;

namespace S4League.Scn;

/// <summary>A parsed .scn scene: a header plus an ordered list of chunks.</summary>
public class SceneContainer : ObservableCollection<SceneChunk>
{
    public static bool Verbose { get; set; }

    public SceneHeader Header { get; set; } = new();

    public List<BoxChunk> Boxes { get; } = new();
    public List<ModelChunk> Models { get; } = new();
    public List<BoneChunk> Bones { get; } = new();
    public List<BoneSystemChunk> BoneSystems { get; } = new();
    public List<SkyDirect1Chunk> SkyDirect1List { get; } = new();
    public List<ShapeChunk> Shapes { get; } = new();

    public SceneContainer() { }

    protected override void InsertItem(int index, SceneChunk item)
    {
        base.InsertItem(index, item);
        switch (item.ChunkType)
        {
            case ChunkType.Box: Boxes.Add((BoxChunk)item); break;
            case ChunkType.ModelData: Models.Add((ModelChunk)item); break;
            case ChunkType.Bone: Bones.Add((BoneChunk)item); break;
            case ChunkType.SkyDirect1: SkyDirect1List.Add((SkyDirect1Chunk)item); break;
            case ChunkType.BoneSystem: BoneSystems.Add((BoneSystemChunk)item); break;
            case ChunkType.Shape: Shapes.Add((ShapeChunk)item); break;
        }
    }

    protected override void ClearItems()
    {
        base.ClearItems();
        Boxes.Clear(); Models.Clear(); Bones.Clear();
        SkyDirect1List.Clear(); BoneSystems.Clear(); Shapes.Clear();
    }

    // ---- Reading ------------------------------------------------------------

    public static SceneContainer ReadFrom(string fileName)
    {
        Log($"Opening file: {fileName}");
        using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadFrom(fs);
    }

    public static SceneContainer ReadFrom(byte[] data)
    {
        using var s = new MemoryStream(data);
        return ReadFrom(s);
    }

    public static SceneContainer ReadFrom(Stream stream)
    {
        var container = new SceneContainer();

        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        container.Header.Deserialize(stream);

        for (int i = 0; i < container.Header.ChunkCount; i++)
        {
            var type = r.ReadEnum<ChunkType>();
            string name = r.ReadCString();
            string subName = r.ReadCString();

            switch (type)
            {
                case ChunkType.ModelData:
                {
                    var model = new ModelChunk(container) { Name = name, SubName = subName };
                    model.Deserialize(stream);
                    container.Add(model);
                    break;
                }
                case ChunkType.Box:
                {
                    var box = new BoxChunk(container) { Name = name, SubName = subName };
                    box.Deserialize(stream);
                    container.Add(box);
                    break;
                }
                case ChunkType.Bone:
                {
                    var bone = new BoneChunk(container) { Name = name, SubName = subName };
                    bone.Deserialize(stream);
                    container.Add(bone);
                    break;
                }
                case ChunkType.BoneSystem:
                {
                    var boneSys = new BoneSystemChunk(container) { Name = name, SubName = subName };
                    boneSys.Deserialize(stream);
                    container.Add(boneSys);
                    break;
                }
                case ChunkType.Shape:
                {
                    var shape = new ShapeChunk(container) { Name = name, SubName = subName };
                    shape.Deserialize(stream);
                    container.Add(shape);
                    break;
                }
                case ChunkType.SkyDirect1:
                {
                    var sky = new SkyDirect1Chunk(container) { Name = name, SubName = subName };
                    sky.Deserialize(stream);
                    container.Add(sky);
                    break;
                }
                default:
                    throw new Exception($"Unknown chunk type: 0x{(int)type:X4} StreamPosition: {r.BaseStream.Position}");
            }
        }

        return container;
    }

    // ---- Writing ------------------------------------------------------------

    public void Write(string fileName)
    {
        using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
        Write(fs);
    }

    public void Write(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Header.ChunkCount = Count;
        w.Serialize(Header);

        foreach (var chunk in this)
        {
            w.WriteEnum(chunk.ChunkType);
            w.WriteCString(chunk.Name);
            w.WriteCString(chunk.SubName);
            w.Serialize(chunk);
        }
    }

    public static void Log(object message)
    {
        if (Verbose)
            Console.WriteLine(message);
    }
}

/// <summary>The fixed header that begins a .scn file.</summary>
public class SceneHeader : IScnSerializable
{
    public const uint c_Version = 1;
    public const uint Magic = 0x6278d57a;

    public string Name { get; set; } = "";
    public string SubName { get; set; } = "";
    public Version Version { get; set; } = Version.One;
    public Version Version2 { get; set; } = Version.Two;
    public Matrix4x4 Matrix { get; set; } = Matrix4x4.Identity;
    public int ChunkCount { get; set; }
    public string AnimCopy { get; set; } = "";

    public void Serialize(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write(c_Version);
        w.Write(Magic);

        w.WriteCString(Name);
        w.WriteCString(SubName);

        w.Write((int)Version);
        w.Write(Matrix);
        w.Write((int)Version2);

        w.Write(ChunkCount);
        if (Version2 >= Version.Two)
            w.WriteCString(AnimCopy);
    }

    public void Deserialize(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        uint value;
        do
        {
            value = r.ReadUInt32();
            if (value != Magic)
                r.BaseStream.Seek(-3, SeekOrigin.Current);
        } while (value != Magic);

        Name = r.ReadCString();
        SubName = r.ReadCString();
        Version = (Version)r.ReadInt32();
        Matrix = r.ReadMatrix();
        Version2 = (Version)r.ReadInt32();
        ChunkCount = (int)r.ReadUInt32();

        if (Version2 == Version.Two)
            AnimCopy = r.ReadCString();
    }
}
