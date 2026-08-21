using System.Numerics;

namespace S4League.Scn;

/// <summary>A Game::CActorGeometry block: mesh geometry, texture references, bone weights and animation.</summary>
public class ModelChunk : SceneChunk
{
    public override ChunkType ChunkType => ChunkType.ModelData;

    public RenderFlag Shader { get; set; } = RenderFlag.None;
    public TextureData TextureData { get; set; }
    public MeshData Mesh { get; set; }
    public IList<WeightBone> WeightBone { get; set; }
    public List<ModelAnimation> Animation { get; set; }

    public ModelChunk(SceneContainer container)
        : base(container)
    {
        TextureData = new TextureData(this);
        Mesh = new MeshData(this);
        WeightBone = new List<WeightBone>();
        Animation = new List<ModelAnimation>();
    }

    public override void Serialize(Stream stream)
    {
        base.Serialize(stream);

        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.WriteEnum(Shader);

        w.Serialize(TextureData);
        w.Serialize(Mesh);

        w.Write(WeightBone.Count);
        w.Serialize(WeightBone);

        w.Write(Animation.Count);
        foreach (var anim in Animation)
        {
            w.WriteCString(anim.Name);
            w.Serialize(anim.TransformKeyData2);
        }
    }

    public override void Deserialize(Stream stream)
    {
        base.Deserialize(stream);

        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Shader = r.ReadEnum<RenderFlag>();

        TextureData = new TextureData(this);
        TextureData.Deserialize(stream);

        Mesh = new MeshData(this);
        Mesh.Deserialize(stream);

        WeightBone = r.DeserializeArray<WeightBone>(r.ReadInt32());

        int count = r.ReadInt32();
        for (int i = 0; i < count; ++i)
            Animation.Add(new ModelAnimation { Name = r.ReadCString(), TransformKeyData2 = r.Deserialize<TransformKeyData2>() });
    }
}

public class MeshData : IScnSerializable
{
    public ModelChunk ModelChunk { get; }

    public List<Vector3> Vertices { get; set; } = new();
    public List<Vector3Int> Faces { get; set; } = new();
    public List<Vector3> Normals { get; set; } = new();
    public List<Vector2> UV { get; set; } = new();
    public List<Vector2> UV2 { get; set; } = new();
    public List<Vector3> Tangents { get; set; } = new();

    public MeshData(ModelChunk modelChunk)
    {
        ModelChunk = modelChunk;
    }

    public void Serialize(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write(Vertices.Count);
        foreach (var v in Vertices) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }

        w.Write(Faces.Count);
        foreach (var f in Faces)
        {
            w.Write((short)f.X); w.Write((short)f.Y); w.Write((short)f.Z);
        }

        w.Write(Normals.Count);
        foreach (var n in Normals) { w.Write(n.X); w.Write(n.Y); w.Write(n.Z); }

        w.Write(UV.Count);
        foreach (var uv in UV) { w.Write(uv.X); w.Write(1 - uv.Y); }

        if (ModelChunk.TextureData.ExtraUV == 1)
            foreach (var uv in UV2) { w.Write(uv.X); w.Write(1 - uv.Y); }

        w.Write(Tangents.Count);
        foreach (var t in Tangents) { w.Write(t.X); w.Write(t.Y); w.Write(t.Z); }
    }

    public void Deserialize(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++)
            Vertices.Add(new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));

        count = r.ReadInt32();
        for (int i = 0; i < count; i++)
            Faces.Add(new Vector3Int(r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16()));

        count = r.ReadInt32();
        for (int i = 0; i < count; i++)
            Normals.Add(new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));

        count = r.ReadInt32();
        for (int i = 0; i < count; i++)
            UV.Add(new Vector2(r.ReadSingle(), 1 - r.ReadSingle()));

        if (ModelChunk.TextureData.ExtraUV == 1)
            for (int i = 0; i < count; i++)
                UV2.Add(new Vector2(r.ReadSingle(), 1 - r.ReadSingle()));

        count = r.ReadInt32();
        for (int i = 0; i < count; i++)
            Tangents.Add(new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
    }

    public int[] Triangles()
    {
        var array = new int[Faces.Count * 3];
        for (int i = 0; i < Faces.Count; i++)
        {
            array[i * 3] = Faces[i].X;
            array[i * 3 + 1] = Faces[i].Y;
            array[i * 3 + 2] = Faces[i].Z;
        }
        return array;
    }
}

/// <summary>Trivial int3 alias replacing Unity's Vector3Int (three ushort indices).</summary>
public readonly struct Vector3Int
{
    public readonly int X, Y, Z;
    public Vector3Int(int x, int y, int z) { X = x; Y = y; Z = z; }
    public override string ToString() => $"({X}, {Y}, {Z})";
}

public class WeightBone : IScnSerializable
{
    public string Name { get; set; } = "";
    public Matrix4x4 Matrix { get; set; } = Matrix4x4.Identity;
    public IList<WeightData> Weight { get; set; } = new List<WeightData>();

    public void Serialize(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.WriteCString(Name);
        w.Write(Matrix);
        w.Write(Weight.Count);
        foreach (var weight in Weight)
        {
            w.Write(weight.Vertex);
            w.Write(weight.Weight);
        }
    }

    public void Deserialize(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Name = r.ReadCString();
        Matrix = r.ReadMatrix();

        uint count = r.ReadUInt32();
        for (int i = 0; i < count; i++)
            Weight.Add(new WeightData { Vertex = r.ReadUInt32(), Weight = r.ReadSingle() });
    }
}

public struct WeightData
{
    public uint Vertex;
    public float Weight;
}

/// <summary>Game::CActorGeomData — texture list with per-submesh face ranges.</summary>
public class TextureData : IScnSerializable
{
    public float Version { get; set; } = 0.2000000029802322f;
    public ModelChunk ModelChunk { get; }
    public uint ExtraUV { get; set; }
    public List<TextureEntry> Textures { get; set; } = new();

    public TextureData(ModelChunk modelChunk)
    {
        ModelChunk = modelChunk;
    }

    public void Serialize(Stream stream)
    {
        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write(Version);
        if (Version >= 0.2000000029802322f)
            w.Write(ExtraUV);

        w.Write(Textures.Count);
        foreach (var texture in Textures)
        {
            w.WriteCString(texture.main_texture);
            if (Version >= 0.2000000029802322f)
                w.WriteCString(texture.side_texture);

            w.Write(texture.face_offset);
            w.Write(texture.face_count);
        }
    }

    public void Deserialize(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Version = r.ReadSingle();

        if (Version >= 0.2000000029802322f)
            ExtraUV = r.ReadUInt32();

        uint count = r.ReadUInt32();
        for (int i = 0; i < count; i++)
        {
            var textureData = new TextureEntry
            {
                main_texture = ReadString(r, 1024),
                side_texture = ""
            };

            if (Version >= 0.2000000029802322f)
                textureData.side_texture = ReadString(r, 1024);

            textureData.face_offset = r.ReadInt32();
            textureData.face_count = r.ReadInt32();

            Textures.Add(textureData);
        }
    }

    static string ReadString(BinaryReader r, int length)
    {
        var chars = new List<char>();
        long position = r.BaseStream.Position;
        for (int i = 0; i < length; i++)
        {
            var c = r.ReadChar();
            if (Convert.ToByte(c) == byte.Parse("0"))
                break;
            chars.Add(c);
        }
        r.BaseStream.Seek(position + 1024, SeekOrigin.Begin);
        return new string(chars.ToArray());
    }
}

public struct TextureEntry
{
    public string main_texture;
    public string side_texture;
    public int face_offset;
    public int face_count;
}
