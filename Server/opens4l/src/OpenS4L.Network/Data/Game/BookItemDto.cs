using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Data.Game
{
    [BlubContract]
    public class BookItemDto
    {
        [BlubMember(0)]
        public int Unk1 { get; set; }

        [BlubMember(1)]
        public int Unk2 { get; set; }

        [BlubMember(2)]
        public byte Unk3 { get; set; }
    }
}
