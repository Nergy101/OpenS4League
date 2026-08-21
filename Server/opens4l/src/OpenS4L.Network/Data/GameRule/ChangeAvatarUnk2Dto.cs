using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Data.GameRule
{
    [BlubContract]
    public class ChangeAvatarUnk2Dto
    {
        [BlubMember(0)]
        public float Unk1 { get; set; }

        [BlubMember(1)]
        public float Unk2 { get; set; }
    }
}
