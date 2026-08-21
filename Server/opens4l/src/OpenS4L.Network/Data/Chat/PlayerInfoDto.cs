using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Data.Chat
{
    [BlubContract]
    public class PlayerInfoDto
    {
        [BlubMember(0)]
        public PlayerInfoShortDto Info { get; set; }

        [BlubMember(1)]
        public PlayerLocationDto Location { get; set; }

        public PlayerInfoDto()
        {
            Info = new PlayerInfoShortDto();
            Location = new PlayerLocationDto();
        }

        public PlayerInfoDto(PlayerInfoShortDto info, PlayerLocationDto location)
        {
            Info = info;
            Location = location;
        }
    }
}
