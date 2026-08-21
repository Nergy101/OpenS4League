using OpenS4L.Server.Game;

namespace OpenS4L.Plugins.WebApi.Models
{
    public class BanRequestDto
    {
        public ulong PlayerId { get; set; }
        public long? Duration { get; set; }
        public string Reason { get; set; }
    }

    public class RoomKickRequestDto
    {
        public ulong PlayerId { get; set; }
        public RoomLeaveReason? Reason { get; set; }
    }

    public class CloseRoomRequestDto
    {
        public uint ChannelId { get; set; }
        public uint RoomId { get; set; }
        public RoomLeaveReason? Reason { get; set; }
    }
}
