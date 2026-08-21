using OpenS4L.Blub.Serialization;
using OpenS4L.Blub.Serialization.Serializers;

namespace OpenS4L.Network.Data.GameRule
{
    [BlubContract]
    public class ItemDropDto
    {
        [BlubMember(0)]
        public int Unk1 { get; set; }

        [BlubMember(1)]
        public int Unk2 { get; set; }

        [BlubMember(2)]
        [BlubSerializer(typeof(FixedArraySerializer), 6)]
        public byte[] Unk3 { get; set; }

        [BlubMember(3)]
        public int Unk4 { get; set; }

        [BlubMember(4)]
        public byte Unk5 { get; set; }

        [BlubMember(5)]
        public short Unk6 { get; set; }

        [BlubMember(6)]
        public short Unk7 { get; set; }

        public ItemDropDto()
        {
            Unk3 = new byte[6];
        }
    }
}
