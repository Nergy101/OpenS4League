using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Data.Game
{
    [BlubContract]
    public class PlayerClubInfoDto
    {
        [BlubMember(0)]
        public uint Unk1 { get; set; }

        [BlubMember(6)]
        public string Unk2 { get; set; }

        [BlubMember(7)]
        public string Unk3 { get; set; }
    }
}
