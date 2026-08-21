using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Logging;
using OpenS4L;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Data;
using OpenS4L.Server.Game.Services;
using OpenS4L.Database;
using OpenS4L.Database.Game;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Builds a real GameDataService populated via reflection (the production loader needs
    /// the absent .x7 data files + Postgres), plus real PlayerItem/RoomCreationOptions objects
    /// for the differential tests.
    /// </summary>
    internal static class GameFixtures
    {
        /// <summary>Constructs a GameDataService and reflection-sets its data collections.</summary>
        public static GameDataService CreateGameDataService()
        {
            // GameDataService ctor does Path.Combine(Program.BaseDirectory, "data"); set it via
            // reflection since Main() (which normally assigns it) never runs in tests.
            var baseDirProp = typeof(Program).GetProperty(
                nameof(Program.BaseDirectory),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            baseDirProp!.SetValue(null, System.IO.Path.GetTempPath());

            // DatabaseService is only dereferenced by LoadShop/LoadLevelRewards, which we don't call.
            var gds = new GameDataService(
                new Logger<GameDataService>(),
                new DatabaseService(EmptyServiceProvider()));

            var itemNumber = new ItemNumber(ItemCategory.Weapon, 1, 1);

            // Effects + effect group (built first: ShopItemInfo ctor reads gameDataService.ShopEffects)
            var effectGroup = new ShopEffectGroup(new ShopEffectGroupEntity
            {
                Id = 20, Name = "fx", PreviewEffect = 1,
                ShopEffects = new List<ShopEffectEntity> { new ShopEffectEntity { Id = 1, Effect = 1 } }
            });

            // Shop price group + price
            var priceGroup = new ShopPriceGroup(new ShopPriceGroupEntity
            {
                Id = 10,
                PriceType = (byte)ItemPriceType.PEN,
                Name = "pen",
                ShopPrices = new List<ShopPriceEntity>
                {
                    new ShopPriceEntity
                    {
                        Id = 100, PriceGroupId = 10, PeriodType = (byte)ItemPeriodType.None,
                        Period = 0, Price = 5000, IsRefundable = true, Durability = 100, IsEnabled = true
                    }
                }
            });

            var shopPrice = new ShopPrice(new ShopPriceEntity
            {
                Id = 100, PriceGroupId = 10, PeriodType = (byte)ItemPeriodType.Days,
                Period = 30, Price = 5000, IsRefundable = true, Durability = 100, IsEnabled = true
            });

            // ItemInfo (built before ShopItem: no ctor dep but needed for the Items dict)
            var itemInfo = new ItemInfo { ItemNumber = itemNumber, Name = "Sword", Gender = Gender.None };

            // Populate the collections ShopItemInfo's ctor reads BEFORE constructing the shop chain.
            Set(gds, "ShopPrices",
                ImmutableDictionary<int, ShopPriceGroup>.Empty.Add(priceGroup.Id, priceGroup));
            Set(gds, "ShopEffects",
                ImmutableDictionary<int, ShopEffectGroup>.Empty.Add(effectGroup.Id, effectGroup));

            // Shop item + item info
            var shopItemEntity = new ShopItemEntity
            {
                Id = itemNumber.Id,
                RequiredGender = (byte)Gender.None,
                ItemInfos = new List<ShopItemInfoEntity>
                {
                    new ShopItemInfoEntity
                    {
                        Id = 1000, ShopItemId = itemNumber.Id,
                        PriceGroupId = 10, EffectGroupId = 20,
                        DiscountPercentage = 0, IsEnabled = true
                    }
                }
            };
            var shopItem = new ShopItem(shopItemEntity, gds);

            var shopItemInfo = new ShopItemInfo(shopItem, shopItemEntity.ItemInfos[0], gds);

            // Reflection-set the remaining collections.
            Set(gds, "Items", ImmutableDictionary<ItemNumber, ItemInfo>.Empty.Add(itemNumber, itemInfo));
            Set(gds, "Maps", ImmutableArray.Create(new MapInfo { Id = 3, Name = "Station", GameRule = GameRule.Practice, IsEnabled = true }));
            Set(gds, "DefaultItems", ImmutableArray.Create(
                DefaultItem(101, CharacterGender.Male, (byte)CostumeSlot.Hair),
                DefaultItem(102, CharacterGender.Female, (byte)CostumeSlot.Hair),
                DefaultItem(201, CharacterGender.Male, (byte)CostumeSlot.Face),
                DefaultItem(202, CharacterGender.Female, (byte)CostumeSlot.Face),
                DefaultItem(301, CharacterGender.Male, (byte)CostumeSlot.Shirt),
                DefaultItem(302, CharacterGender.Female, (byte)CostumeSlot.Shirt),
                DefaultItem(401, CharacterGender.Male, (byte)CostumeSlot.Pants),
                DefaultItem(402, CharacterGender.Female, (byte)CostumeSlot.Pants),
                DefaultItem(501, CharacterGender.Male, (byte)CostumeSlot.Gloves),
                DefaultItem(502, CharacterGender.Female, (byte)CostumeSlot.Gloves),
                DefaultItem(601, CharacterGender.Male, (byte)CostumeSlot.Shoes),
                DefaultItem(602, CharacterGender.Female, (byte)CostumeSlot.Shoes)));
            Set(gds, "ShopItems",
                ImmutableDictionary<ItemNumber, ShopItem>.Empty.Add(itemNumber, shopItem));
            Set(gds, "Levels", ImmutableDictionary<int, LevelInfo>.Empty
                .Add(0, new LevelInfo { Level = 0, ExperienceToNextLevel = 100, TotalExperience = 0 })
                .Add(1, new LevelInfo { Level = 1, ExperienceToNextLevel = 200, TotalExperience = 100 })
                .Add(2, new LevelInfo { Level = 2, ExperienceToNextLevel = 0, TotalExperience = 300 }));
            Set(gds, "LevelRewards", ImmutableDictionary<int, LevelReward>.Empty);
            Set(gds, "GameTempos", ImmutableDictionary<string, GameTempo>.Empty
                .Add("GAMETEMPO_FREE", new GameTempo { ActorDefaultHPMax = 100 }));
            Set(gds, "Effects", ImmutableDictionary<uint, ItemEffect>.Empty);
            Set(gds, "EquipLimits", ImmutableDictionary<int, EquipLimitInfo>.Empty);

            // Store for building PlayerItem
            Stash.ShopItemInfo = shopItemInfo;
            Stash.ShopPrice = shopPrice;
            Stash.GameDataService = gds;

            return gds;
        }

        public static PlayerItem CreatePlayerItem(GameDataService gds)
        {
            var inventory = new PlayerInventory(
                new Logger<PlayerInventory>(),
                gds,
                new OpenS4L.Common.IdGeneratorService(
                    Microsoft.Extensions.Options.Options.Create(new OpenS4L.Common.Configuration.IdGeneratorOptions { Id = 1 })),
                new OpenS4L.Server.Game.Mappers.GameMapper());

            return new PlayerItem(
                gds, inventory, 9001,
                Stash.ShopItemInfo, Stash.ShopPrice,
                color: 2, effects: new uint[] { 1 }, purchaseDate: DateTimeOffset.FromUnixTimeSeconds(1700000000));
        }

        public static RoomCreationOptions CreateRoomCreationOptions()
        {
            return new RoomCreationOptions
            {
                Name = "TestRoom",
                GameRule = GameRule.Practice,
                Map = 1,
                PlayerLimit = 8,
                SpectatorLimit = 2,
                TimeLimit = TimeSpan.FromMinutes(10),
                ScoreLimit = 50,
                Password = "secret",
                EquipLimit = 3,
                RelayEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 23000),
                IsFriendly = true
            };
        }

        /// <summary>
        /// Seeds a shop item into the GameDataService so Inventory.Create can resolve it
        /// (the login flow's SendAccountInformation creates the slot-coupon item 6000015).
        /// </summary>
        public static void SeedShopItem(GameDataService gds, ItemNumber itemNumber, int priceGroupId = 10)
        {
            var entity = new ShopItemInfoEntity
            {
                Id = (int)itemNumber.Id + 1000, ShopItemId = itemNumber.Id,
                PriceGroupId = priceGroupId, EffectGroupId = 20,
                DiscountPercentage = 0, IsEnabled = true
            };
            var shopItem = new ShopItem(new ShopItemEntity
            {
                Id = itemNumber.Id,
                RequiredGender = (byte)Gender.None,
                ItemInfos = new List<ShopItemInfoEntity> { entity }
            }, gds);
            var info = new ShopItemInfo(shopItem, entity, gds);
            var itemInfo = new ItemInfo { ItemNumber = itemNumber, Name = "Coupon", Gender = Gender.None };

            // Reflectively extend the collections.
            var items = gds.Items.Add(itemNumber, itemInfo);
            var shopItems = gds.ShopItems.Add(itemNumber, shopItem);
            Set(gds, "Items", items);
            Set(gds, "ShopItems", shopItems);
            Stash.ShopItemInfo = info;
        }

        /// <summary>Builds a DefaultItem whose SubCategory encodes the costume slot.</summary>
        private static DefaultItem DefaultItem(uint id, CharacterGender gender, byte subCategory)
        {
            // ItemNumber: Category(Costume=1)*1_000_000 + SubCategory*10_000 + Number.
            return new DefaultItem
            {
                ItemNumber = new ItemNumber((uint)(1_000_000 + subCategory * 10_000 + id)),
                Gender = gender,
                Variation = 0
            };
        }

        public static OpenS4L.Server.Game.Mappers.GameMapper CreateMapper() => new();

        private static void Set(object obj, string prop, object value)
        {
            var p = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            p!.SetValue(obj, value);
        }

        private static IServiceProvider EmptyServiceProvider()
        {
            return new EmptyProvider();
        }

        private class EmptyProvider : IServiceProvider
        {
            public object GetService(Type serviceType) => null;
        }

        /// <summary>Thread-local stash for objects built while creating the GameDataService.</summary>
        internal static class Stash
        {
            [ThreadStatic] public static ShopItemInfo ShopItemInfo;
            [ThreadStatic] public static ShopPrice ShopPrice;
            [ThreadStatic] public static GameDataService GameDataService;
        }
    }
}
