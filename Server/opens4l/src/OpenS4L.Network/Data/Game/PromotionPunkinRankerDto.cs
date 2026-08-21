using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Data.Game
{
    [BlubContract]
    public class PromotionPunkinRankerDto
    {
        [BlubMember(0)]
        public string Unk1 { get; set; }

        [BlubMember(1)]
        public int Unk2 { get; set; }
    }
}
