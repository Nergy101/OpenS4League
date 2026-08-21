namespace S4League.Scn;

/// <summary>Scene container / file version constants used by the S4 .scn format.</summary>
public enum Version : int
{
    One = 1036831949, // 0x3DC0004D
    Two = 1045220557  // 0x3E4CCCCD
}

/// <summary>Identifies the block type of each top-level entry in a .scn file.</summary>
public enum ChunkType : uint
{
    Box         = 0x25ADF0D1, // fumbi, spawns, deadzones
    ModelData   = 0x081098F8, // Game::CActorGeometry
    Bone        = 0x6D411AD1, // CoreLib::Scene::CBone
    SkyDirect1  = 0xC3E8BE62,
    BoneSystem  = 0x5E74333F,
    Shape       = 0xADEE38A2
}

/// <summary>Rendering flags attached to a model chunk (shader / blend state).</summary>
[Flags]
public enum RenderFlag : int
{
    None        = 0,
    NoLight     = 1,
    Transparent = 2,
    Cutout      = 4,
    NoCulling   = 8,
    Billboard   = 16,
    Flare       = 32,
    ZWriteOff   = 64,
    Shader      = 128,
    NoPrerender = 256,
    NoFog       = 512,
    Unknown     = 1024,
    NoMipmap    = 2048,
    VertexAnim  = 4096,
    Shadow      = 8192,
    Glow        = 16384,
    Water       = 32768,
    Distortion  = 65536,
    Dark        = 131072,
    Unk1        = 262144,
    Unk2        = 524288,
    Unk3        = 1048576
}

/// <summary>Legacy shading enum (unused by the parser; kept for reference).</summary>
[Flags]
public enum Shader : int
{
    None       = 0,
    NoLight    = 1,
    Transparent = 2,
    Cutout     = 4,
    NoCulling  = 8,
    Billboard  = 16,
    Flare      = 32,
    ZWriteOff  = 64,
    Shader     = 128,
    NoFog      = 512,
    Unknown    = 1024,
    NoMipmap   = 2048,
    VertexAnim = 4096,
    Shadow     = 8192,
    Glow       = 16384,
    Water      = 32768,
    Distortion = 65536,
    Dark       = 131072
}

public enum ParentGrade : uint
{
    Father,
    Child,
    Grandson
}
