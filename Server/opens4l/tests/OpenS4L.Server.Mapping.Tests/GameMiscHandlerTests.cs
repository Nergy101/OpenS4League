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
    /// Drives the Game MiscHandler (time sync, admin window, admin action) over the harness.
    /// </summary>
    public class GameMiscHandlerTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();

        private async Task<(Player plr, FakeSocketChannel channel)> LoginAsync(uint accountId, SecurityLevel level = SecurityLevel.User)
        {
            var cache = (Foundatio.Caching.InMemoryCacheClient)_ctx.Get<Foundatio.Caching.ICacheClient>();
            await cache.SetAsync<string>(Constants.Cache.SessionKey(accountId), "sid-" + accountId);
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = (int)accountId, Username = "g" + accountId, Nickname = "nick" + accountId, SecurityLevel = (byte)level });
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
        public async Task TimeSync_acknowledges()
        {
            var (plr, channel) = await LoginAsync(2001);
            var handler = _ctx.Get<MiscHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new TimeSyncReqMessage { Time = 123 });
            Assert.Contains(channel.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is TimeSyncAckMessage);
        }

        [Fact]
        public async Task AdminShowWindow_user_returnsTrue()
        {
            var (plr, channel) = await LoginAsync(2002, SecurityLevel.User);
            var handler = _ctx.Get<MiscHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new AdminShowWindowReqMessage());
            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<AdminShowWindowAckMessage>().LastOrDefault();
            Assert.NotNull(ack);
            Assert.True(ack.DisableConsole); // user → console disabled
        }

        [Fact]
        public async Task AdminShowWindow_gm_returnsFalse()
        {
            var (plr, channel) = await LoginAsync(2003, SecurityLevel.GameMaster);
            var handler = _ctx.Get<MiscHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new AdminShowWindowReqMessage());
            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<AdminShowWindowAckMessage>().LastOrDefault();
            Assert.NotNull(ack);
            Assert.False(ack.DisableConsole); // GM → console enabled
        }
    }
}
