using System;
using System.Linq;
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
    /// Drives the Game PlayerInventory (create/errors/remove/contains/save) over the harness.
    /// </summary>
    public class GamePlayerInventoryTests
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
        public async Task Inventory_createGetRemove()
        {
            var plr = await LoginAsync(2701);
            var inv = plr.Inventory;

            var item = inv.Create((ItemNumber)2010001u, ItemPriceType.PEN, ItemPeriodType.None, 0, 0, Array.Empty<uint>(), 1, false);
            Assert.NotNull(item);
            Assert.True(inv.Contains(item.Id));
            Assert.Same(item, inv.GetItem(item.Id));
            Assert.Same(item, inv[item.Id]);

            Assert.True(inv.Remove(item));
            Assert.False(inv.Contains(item.Id));
        }

        [Fact]
        public async Task Inventory_createErrors()
        {
            var plr = await LoginAsync(2702);
            var inv = plr.Inventory;

            Assert.Throws<ArgumentException>(() => inv.Create((ItemNumber)9999999u, ItemPriceType.PEN, ItemPeriodType.None, 0, 0, Array.Empty<uint>(), 1, false));
            Assert.Throws<ArgumentException>(() => inv.Create((ItemNumber)2010001u, ItemPriceType.PEN, ItemPeriodType.Days, 5, 0, Array.Empty<uint>(), 1, false));
        }

        [Fact]
        public async Task Inventory_removeMissing_returnsFalse()
        {
            var plr = await LoginAsync(2703);
            Assert.False(plr.Inventory.Remove(999999));
        }
    }
}
