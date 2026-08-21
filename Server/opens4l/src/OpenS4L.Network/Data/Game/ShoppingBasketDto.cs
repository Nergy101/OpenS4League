using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Data.Game
{
    [BlubContract]
    public class ShoppingBasketDto
    {
        [BlubMember(0)]
        public ulong ItemId { get; set; }

        [BlubMember(1)]
        public ShopItemDto ShopItem { get; set; }
    }
}
