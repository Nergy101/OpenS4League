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
using OpenS4L.Plugins.WebApi.Mappers;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Drives the WebApiMapper full DTO paths (player, character, room) over a logged-in Game
    /// player with a character + item + room.
    /// </summary>
    public class WebApiMapperTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();
        private readonly WebApiMapper _mapper;

        public WebApiMapperTests()
        {
            _mapper = new WebApiMapper(_ctx.GameData);
        }

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
        public async Task ToPlayerDto_withItem()
        {
            var plr = await LoginAsync(3201);
            plr.Inventory.Create((ItemNumber)2010001u, ItemPriceType.PEN, ItemPeriodType.None, 0, 0, Array.Empty<uint>(), 1, false);

            var dto = _mapper.ToPlayerDto(plr);
            Assert.Equal(3201ul, dto.Id);
            Assert.Equal("nick3201", dto.Nickname);
            Assert.NotEmpty(dto.Inventory);
        }

        [Fact]
        public async Task ToRoomPlayerDto_includesTeam()
        {
            var plr = await LoginAsync(3202);
            var dto = _mapper.ToRoomPlayerDto(plr);
            Assert.Equal(3202ul, dto.Id);
            Assert.Equal("nick3202", dto.Nickname);
        }

        [Fact]
        public async Task ToMapDto_and_ToItemDto()
        {
            var map = _ctx.GameData.Maps.First();
            var mapDto = _mapper.ToMapDto(map);
            Assert.Equal(map.Id, mapDto.Id);
            Assert.Equal(map.Name, mapDto.Name);

            var item = _ctx.GameData.Items.Values.First();
            var itemDto = _mapper.ToItemDto(item);
            Assert.Equal(item.ItemNumber.Id, itemDto.Id);
            Assert.Equal(item.Name, itemDto.Name);
        }

        [Fact]
        public async Task ToPlayerItemDto_maps()
        {
            var plr = await LoginAsync(3203);
            var item = plr.Inventory.Create((ItemNumber)2010001u, ItemPriceType.PEN, ItemPeriodType.None, 0, 0, Array.Empty<uint>(), 1, false);

            var itemDto = _mapper.ToPlayerItemDto(item);
            Assert.Equal(item.Id, itemDto.Id);
            Assert.Equal((uint)2010001, itemDto.Item.Id);
            Assert.Equal(ItemPriceType.PEN, itemDto.PriceType);
            Assert.Equal(ItemPeriodType.None, itemDto.PeriodType);
        }
    }
}
