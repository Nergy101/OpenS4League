using System;

namespace OpenS4L.Server.Game
{
    public class ChannelJoinHookEventArgs : EventArgs
    {
        public Channel Channel { get; }
        public Player Player { get; }
        public ChannelJoinError Error { get; set; }

        public ChannelJoinHookEventArgs(Channel channel, Player player)
        {
            Channel = channel;
            Player = player;
            Error = ChannelJoinError.OK;
        }
    }
}
