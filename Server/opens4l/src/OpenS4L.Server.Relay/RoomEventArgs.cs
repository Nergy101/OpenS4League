using System;

namespace OpenS4L.Server.Relay
{
    public class RoomEventArgs : EventArgs
    {
        public Room Room { get; }

        public RoomEventArgs(Room room)
        {
            Room = room;
        }
    }
}
