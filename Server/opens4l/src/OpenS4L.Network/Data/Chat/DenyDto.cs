using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Data.Chat
{
    [BlubContract]
    public class DenyDto
    {
        [BlubMember(0)]
        public ulong AccountId { get; set; }

        [BlubMember(1)]
        public string Nickname { get; set; }

        public DenyDto()
        {
            Nickname = "";
        }
    }
}
