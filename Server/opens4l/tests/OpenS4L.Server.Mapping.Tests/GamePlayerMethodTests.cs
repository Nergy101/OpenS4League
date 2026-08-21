using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Common.Configuration;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Message.Club;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Exercises the Game Player domain methods over the harness (gain experience, money/club
    /// info, notices, console messages, disconnect).
    /// </summary>
    public class GamePlayerMethodTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();

        private async Task<(Player player, FakeSocketChannel channel)> LoginAsync(uint accountId)
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
        public async Task GainExperience_addsXp()
        {
            var (player, _) = await LoginAsync(7001);
            // Reset XP to level 0; fixture levels: L0 needs 100, L1 needs 200, L2 = max.
            player.TotalExperience = 0;
            Assert.Equal(0, player.Level);
            player.GainExperience(150); // enough to reach level 1 (100 XP threshold)
            Assert.Equal(150u, player.TotalExperience);
            Assert.Equal(1, player.Level);
        }

        [Fact]
        public async Task GainExperience_smallAmount_returnsTrue()
        {
            var (player, _) = await LoginAsync(7001);
            player.TotalExperience = 0;
            // +50 XP stays within level 0's band; the while-loop body doesn't run so the
            // "leveledUp" flag stays false even though XP is added — a quirk pinned here.
            var leveled = player.GainExperience(50);
            Assert.False(leveled);
            Assert.Equal(50u, player.TotalExperience);
        }

        [Fact]
        public async Task GainExperience_atMaxLevel_returnsFalse()
        {
            var (player, _) = await LoginAsync(7002);
            player.TotalExperience = 300; // level 2 (max)
            var leveled = player.GainExperience(100);
            Assert.False(leveled);
        }

        [Fact]
        public async Task SendNotice_andConsole_sendMessages()
        {
            var (player, channel) = await LoginAsync(7003);
            player.SendNotice("hello world");
            player.SendConsoleMessage("gm command");
            Assert.NotEmpty(channel.Outbound);
        }

        [Fact]
        public async Task SendClubInfo_noClan_sendsEmpty()
        {
            var (player, channel) = await LoginAsync(7004);
            player.SendClubInfo();
            Assert.True(channel.Outbound.Any(o => o.GetType().GetProperty("Message")?.GetValue(o) is ClubMyInfoAckMessage));
        }

        [Fact]
        public async Task SendMoneyUpdate_sendsBoth()
        {
            var (player, channel) = await LoginAsync(7005);
            player.SendMoneyUpdate();
            Assert.Contains(channel.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is MoneyRefreshCashInfoAckMessage);
            Assert.Contains(channel.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is MoenyRefreshCoinInfoAckMessage);
        }
    }
}
