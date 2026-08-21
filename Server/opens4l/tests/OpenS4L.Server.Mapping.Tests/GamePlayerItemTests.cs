using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Drives the Game PlayerItem domain (effects/shop lookups/durability) over the harness.
    /// </summary>
    public class GamePlayerItemTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();

        private async Task<Player> LoginAsync(uint accountId)
        {
            var cache = (Foundatio.Caching.InMemoryCacheClient)_ctx.Get<Foundatio.Caching.ICacheClient>();
            await cache.SetAsync<string>(Constants.Cache.SessionKey(accountId), "sid-" + accountId);
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = (int)accountId, Username = "g" + accountId, Nickname = "nick" + accountId });
                await auth.SaveChangesAsync();
            }
            using (var db = _ctx.Get<GameContext>())
            {
                db.Players.Add(new PlayerEntity { Id = (int)accountId, TotalExperience = 1000 });
                await db.SaveChangesAsync();
            }

            var handler = _ctx.Get<AuthenticationHandler>();
            var (session, channel) = _ctx.CreateSession(accountId);
            await handler.OnHandle(new MessageContext { Session = session }, new LoginRequestReqMessage
            {
                AccountId = accountId, SessionId = "sid-" + accountId, Version = new Version(1, 0, 0, 0)
            });
            return session.Player;
        }

        [Fact]
        public async Task PlayerItem_lookupsAndDurability()
        {
            var plr = await LoginAsync(2601);
            // Create a weapon item via the inventory.
            var item = plr.Inventory.Create((ItemNumber)2010001u, ItemPriceType.PEN, ItemPeriodType.None, 0, 0, Array.Empty<uint>(), 1, false);
            Assert.NotNull(item);

            // Shop lookups.
            Assert.NotNull(item.GetShopItem());
            Assert.NotNull(item.GetShopItemInfo());
            Assert.NotNull(item.GetShopPrice());
            Assert.Null(item.GetItemEffects()); // no effects → null

            // Durability.
            Assert.Throws<ArgumentOutOfRangeException>(() => item.LoseDurability(-1));
            Assert.Throws<InvalidOperationException>(() => item.LoseDurability(1)); // not in a room

            // ExpireDate for a None-period item is MinValue.
            Assert.Equal(DateTimeOffset.MinValue, item.ExpireDate);
        }
    }
}
