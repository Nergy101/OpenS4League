using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Data.Club
{
    [BlubContract]
    public class AdminGradeDto
    {
        [BlubMember(0)]
        public ulong AccountId { get; set; }

        [BlubMember(1)]
        public ClubRole Role { get; set; }
    }
}
