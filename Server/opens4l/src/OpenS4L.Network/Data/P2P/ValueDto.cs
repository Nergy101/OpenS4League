using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Data.P2P
{
    [BlubContract]
    public class ValueDto
    {
        [BlubMember(0)]
        public float Value1 { get; set; }

        [BlubMember(1)]
        public float Value2 { get; set; }
    }
}
