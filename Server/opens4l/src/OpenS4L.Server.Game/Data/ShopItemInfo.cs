using OpenS4L.Database.Game;
using OpenS4L.Server.Game.Services;

namespace OpenS4L.Server.Game.Data
{
    public class ShopItemInfo
    {
        public int Id { get; set; }
        public ShopPriceGroup PriceGroup { get; set; }
        public ShopEffectGroup EffectGroup { get; set; }
        public bool IsEnabled { get; set; }
        public int Discount { get; set; }

        public ShopItem ShopItem { get; }

        public ShopItemInfo(ShopItem shopItem, ShopItemInfoEntity entity, GameDataService gameDataService)
        {
            Id = entity.Id;
            PriceGroup = gameDataService.ShopPrices[entity.PriceGroupId];
            EffectGroup = gameDataService.ShopEffects[entity.EffectGroupId];
            IsEnabled = entity.IsEnabled;
            Discount = entity.DiscountPercentage;

            ShopItem = shopItem;
        }
    }
}
