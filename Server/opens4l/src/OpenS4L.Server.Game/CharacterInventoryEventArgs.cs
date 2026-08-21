using System;

namespace OpenS4L.Server.Game
{
    public class CharacterInventoryEventArgs : EventArgs
    {
        public byte Slot { get; }
        public PlayerItem Item { get; }

        public CharacterInventoryEventArgs(byte slot, PlayerItem item)
        {
            Slot = slot;
            Item = item;
        }
    }
}
