using System;

namespace OpenS4L.Server.Relay
{
    public class PlayerEventArgs : EventArgs
    {
        public Player Player { get; }

        public PlayerEventArgs(Player player)
        {
            Player = player;
        }
    }
}
