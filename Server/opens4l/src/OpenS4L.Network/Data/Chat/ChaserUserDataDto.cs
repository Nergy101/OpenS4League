using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Data.Chat
{
    [BlubContract]
    public class ChaserUserDataDto
    {
        [BlubMember(0)]
        public float KillProbability { get; set; }

        [BlubMember(1)]
        public float Kills { get; set; }
    }
}
