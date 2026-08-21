using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using OpenS4L.Server.Game.Services;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Drives the Game ChannelHandler (enter/info/leave) over the harness.
    /// </summary>
    public class GameChannelHandlerTests
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

        private async Task SeedChannelAsync()
        {
            using (var db = _ctx.Get<GameContext>())
            {
                db.Channels.Add(new ChannelEntity { Id = 1, Name = "Channel 1", Description = "desc", PlayerLimit = 32, Color = "FF0000", MinLevel = 0, MaxLevel = 99 });
                await db.SaveChangesAsync();
            }
            await _ctx.Get<ChannelService>().StartAsync(System.Threading.CancellationToken.None);
        }

        [Fact]
        public async Task ChannelEnter_success()
        {
            await SeedChannelAsync();
            var (plr, channel) = await LoginAsync(1601);

            var handler = _ctx.Get<ChannelHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ChannelEnterReqMessage { Channel = 1 });

            Assert.NotNull(plr.Channel);
            Assert.Equal(1u, plr.Channel.Id);
            Assert.Contains(channel.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is ServerResultAckMessage);
        }

        [Fact]
        public async Task ChannelEnter_nonexistent_returnsError()
        {
            await SeedChannelAsync();
            var (plr, channel) = await LoginAsync(1602);

            var handler = _ctx.Get<ChannelHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ChannelEnterReqMessage { Channel = 99 });

            Assert.Null(plr.Channel);
            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<ServerResultAckMessage>().Last();
            Assert.Equal(ServerResult.NonExistingChannel, ack.Result);
        }

        [Fact]
        public async Task ChannelInfo_channelList()
        {
            await SeedChannelAsync();
            var (plr, channel) = await LoginAsync(1603);

            var handler = _ctx.Get<ChannelHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ChannelInfoReqMessage { Request = ChannelInfoRequest.ChannelList });

            Assert.Contains(channel.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is ChannelListInfoAckMessage);
        }

        [Fact]
        public async Task ChannelLeave_leaves()
        {
            await SeedChannelAsync();
            var (plr, _) = await LoginAsync(1604);

            var handler = _ctx.Get<ChannelHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ChannelEnterReqMessage { Channel = 1 });
            Assert.NotNull(plr.Channel);

            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ChannelLeaveReqMessage { Channel = 1 });
            Assert.Null(plr.Channel);
        }
    }
}
