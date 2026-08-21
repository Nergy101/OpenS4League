using System;

namespace OpenS4L.Server.Game
{
    public class ClanMemberEventArgs : EventArgs
    {
        public ClanMember Member { get; }

        public ClanMemberEventArgs(ClanMember member)
        {
            Member = member;
        }
    }
}
