using System.Numerics;
using System.Text;

namespace S4League.Scn;

/// <summary>
/// Serializable block interface mirroring wtfblub's BlubLib IManualSerializer.
/// Deserialize/Serialize receive the underlying stream (a BinaryReader/Writer is
/// opened with the stream left open, matching BlubLib's ToBinaryReader(leaveOpen)).
/// </summary>
public interface IScnSerializable
{
    void Deserialize(Stream stream);
    void Serialize(Stream stream);
}

/// <summary>
/// BinaryReader/Writer helpers replicating the subset of BlubLib.IO extensions that
/// the original UnityScnTool parser relies on (ReadCString, ReadMatrix, DeserializeArray…).
/// </summary>
public static class ScnReader
{
    /// <summary>Reads a null-terminated UTF-8 string.</summary>
    public static string ReadCString(this BinaryReader r)
    {
        var bytes = new List<byte>();
        byte b;
        while ((b = r.ReadByte()) != 0)
            bytes.Add(b);
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public static void WriteCString(this BinaryWriter w, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        w.Write(bytes);
        w.Write((byte)0);
    }

    /// <summary>
    /// Reads a 4x4 float matrix. The file stores it column-major (as a Unity Matrix4x4
    /// of four column vectors). To be usable with System.Numerics' row-vector
    /// Vector3.Transform, the returned matrix is the transposition, which equates to
    /// simply reading the 16 floats sequentially into the row-major constructor.
    /// </summary>
    public static Matrix4x4 ReadMatrix(this BinaryReader r)
    {
        return new Matrix4x4(
            r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(),
            r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(),
            r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(),
            r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }

    public static void Write(this BinaryWriter w, Matrix4x4 value)
    {
        // value is already the transposed System.Numerics form; writing its elements
        // in row-major order reproduces the on-disk column-major layout.
        w.Write(value.M11); w.Write(value.M12); w.Write(value.M13); w.Write(value.M14);
        w.Write(value.M21); w.Write(value.M22); w.Write(value.M23); w.Write(value.M24);
        w.Write(value.M31); w.Write(value.M32); w.Write(value.M33); w.Write(value.M34);
        w.Write(value.M41); w.Write(value.M42); w.Write(value.M43); w.Write(value.M44);
    }

    public static Vector3 ReadVector3(this BinaryReader r)
        => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

    public static void Write(this BinaryWriter w, Vector3 value)
    {
        w.Write(value.X); w.Write(value.Y); w.Write(value.Z);
    }

    public static Vector2 ReadVector2(this BinaryReader r)
        => new(r.ReadSingle(), r.ReadSingle());

    public static void Write(this BinaryWriter w, Vector2 value)
    {
        w.Write(value.X); w.Write(value.Y);
    }

    public static T ReadEnum<T>(this BinaryReader r) where T : struct, Enum
        => (T)Enum.ToObject(typeof(T), r.ReadUInt32());

    public static void WriteEnum<T>(this BinaryWriter w, T value) where T : struct, Enum
        => w.Write(Convert.ToUInt32(value));

    public static void Write(this BinaryWriter w, Color value)
    {
        w.Write(value.R); w.Write(value.G); w.Write(value.B); w.Write(value.A);
    }

    public static T Deserialize<T>(this BinaryReader r) where T : IScnSerializable, new()
    {
        var t = new T();
        t.Deserialize(r.BaseStream);
        return t;
    }

    public static IList<T> DeserializeArray<T>(this BinaryReader r, int count) where T : IScnSerializable, new()
    {
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            var t = new T();
            t.Deserialize(r.BaseStream);
            list.Add(t);
        }
        return list;
    }

    public static void Serialize<T>(this BinaryWriter w, T value) where T : IScnSerializable
        => value.Serialize(w.BaseStream);

    public static void Serialize<T>(this BinaryWriter w, IList<T> values) where T : IScnSerializable
    {
        foreach (var v in values)
            v.Serialize(w.BaseStream);
    }
}
