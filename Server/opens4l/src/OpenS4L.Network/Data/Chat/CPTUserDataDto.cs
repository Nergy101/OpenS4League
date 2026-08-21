using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Data.Chat
{
    [BlubContract]
    public class CPTUserDataDto
    {
        [BlubMember(0)]
        public float Kills { get; set; }

        [BlubMember(1)]
        public uint Domination { get; set; }
    }
}
