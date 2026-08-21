namespace S4League.Scn;

/// <summary>A skeleton node (CoreLib::Scene::CBone) with optional animation data.</summary>
public class BoneChunk : SceneChunk
{
    public override ChunkType ChunkType => ChunkType.Bone;

    public List<BoneAnimation> Animation { get; set; } = new();

    public BoneChunk(SceneContainer container)
        : base(container) { }

    public override void Serialize(Stream stream)
    {
        base.Serialize(stream);

        using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write(Animation.Count);
        foreach (var anim in Animation)
        {
            if (Version2 == Version.Two)
            {
                w.WriteCString(anim.Name);
                w.WriteCString(anim.Copy ?? "");
                if (string.IsNullOrWhiteSpace(anim.Copy) && anim.TransformKeyData != null)
                    w.Serialize(anim.TransformKeyData);
            }
            else
            {
                w.WriteCString(anim.Name);
                if (anim.TransformKeyData != null)
                    w.Serialize(anim.TransformKeyData);
            }
        }
    }

    public override void Deserialize(Stream stream)
    {
        base.Deserialize(stream);

        using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        uint count = r.ReadUInt32();
        for (int i = 0; i < count; i++)
        {
            if (Version2 == Version.Two)
            {
                string name1 = r.ReadCString();
                string subName = r.ReadCString();

                TransformKeyData? transformKeyData = null;
                if (string.IsNullOrWhiteSpace(subName))
                    transformKeyData = r.Deserialize<TransformKeyData>();

                Animation.Add(new BoneAnimation { Name = name1, Copy = subName, TransformKeyData = transformKeyData });
            }
            else
            {
                string name2 = r.ReadCString();
                Animation.Add(new BoneAnimation { Name = name2, Copy = null, TransformKeyData = r.Deserialize<TransformKeyData>() });
            }
        }
    }
}
