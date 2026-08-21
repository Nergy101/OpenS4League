using System;

namespace OpenS4L.Server.Game
{
    public class TeamEventArgs : EventArgs
    {
        public Team Team { get; }
        public Player Player { get; }

        public TeamEventArgs(Team team, Player player)
        {
            Team = team;
            Player = player;
        }
    }
}
