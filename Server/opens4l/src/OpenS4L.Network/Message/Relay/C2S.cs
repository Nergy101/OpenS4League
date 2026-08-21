using OpenS4L.Blub.Serialization;
using OpenS4L.Network.Data.Relay;

namespace OpenS4L.Network.Message.Relay
{
    [BlubContract]
    public class CRequestLoginMessage : IRelayMessage
    {
        [BlubMember(0)]
        public ulong AccountId { get; set; }

        [BlubMember(1)]
        public string Nickname { get; set; }

        [BlubMember(2)]
        public RoomLocation RoomLocation { get; set; }

        [BlubMember(3)]
        public bool CreatedRoom { get; set; }
    }

    [BlubContract]
    public class CNotifyP2PLogMessage : IRelayMessage
    {
        [BlubMember(0)]
        public int Unk1 { get; set; }

        [BlubMember(1)]
        public short Unk2 { get; set; }

        [BlubMember(2)]
        public byte Unk3 { get; set; }
    }
}
