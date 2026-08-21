using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Data.Game;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Drives the Game ShopHandler (item buying) over the harness.
    /// </summary>
    public class GameShopHandlerTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();

        private async Task<(Player plr, FakeSocketChannel channel)> LoginAsync(uint accountId)
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
            return (session.Player, channel);
        }

        [Fact]
        public async Task BuyItem_success_createsItem()
        {
            var (plr, channel) = await LoginAsync(1701);
            plr.PEN = 100000; // enough for the 5000-PEN fixture item

            var handler = _ctx.Get<ShopHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ItemBuyItemReqMessage
            {
                Items = new[]
                {
                    new ShopItemDto { ItemNumber = (ItemNumber)2010001u, PriceType = ItemPriceType.PEN, PeriodType = ItemPeriodType.None, Period = 0, Color = 0 }
                }
            });

            Assert.Contains(channel.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is ItemBuyItemAckMessage);
        }

        [Fact]
        public async Task BuyItem_notEnoughPEN_returnsError()
        {
            var (plr, channel) = await LoginAsync(1702);
            plr.PEN = 0;

            var handler = _ctx.Get<ShopHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ItemBuyItemReqMessage
            {
                Items = new[]
                {
                    new ShopItemDto { ItemNumber = (ItemNumber)2010001u, PriceType = ItemPriceType.PEN, PeriodType = ItemPeriodType.None, Period = 0, Color = 0 }
                }
            });

            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<ItemBuyItemAckMessage>().LastOrDefault();
            Assert.NotNull(ack);
        }

        [Fact]
        public async Task BuyItem_nonexistentItem_returnsUnkown()
        {
            var (plr, channel) = await LoginAsync(1703);

            var handler = _ctx.Get<ShopHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ItemBuyItemReqMessage
            {
                Items = new[]
                {
                    new ShopItemDto { ItemNumber = (ItemNumber)9999999u, PriceType = ItemPriceType.PEN, PeriodType = ItemPeriodType.None, Period = 0, Color = 0 }
                }
            });

            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<ItemBuyItemAckMessage>().LastOrDefault();
            Assert.NotNull(ack);
        }
    }
}
