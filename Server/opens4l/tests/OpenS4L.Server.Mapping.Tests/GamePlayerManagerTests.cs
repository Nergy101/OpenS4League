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
    /// Drives the Game PlayerManager (add/get/contains/remove/get-by-nickname) over the harness.
    /// </summary>
    public class GamePlayerManagerTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();

        private async Task<Player> LoginAsync(uint accountId, string nickname = null)
        {
            var cache = (Foundatio.Caching.InMemoryCacheClient)_ctx.Get<Foundatio.Caching.ICacheClient>();
            await cache.SetAsync<string>(Constants.Cache.SessionKey(accountId), "sid-" + accountId);
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = (int)accountId, Username = "g" + accountId, Nickname = nickname ?? ("nick" + accountId) });
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
        public async Task PlayerManager_addContainsGetRemove()
        {
            var plr = await LoginAsync(2501);
            var pm = _ctx.Get<PlayerManager>();

            Assert.True(pm.Contains(plr.Account.Id));
            Assert.Same(plr, pm.Get(plr.Account.Id));
            Assert.Same(plr, pm[plr.Account.Id]);
            Assert.Contains(plr, pm);

            pm.Remove(plr.Account.Id);
            Assert.False(pm.Contains(plr.Account.Id));
        }

        [Fact]
        public async Task PlayerManager_addDuplicate_throws()
        {
            var plr = await LoginAsync(2502);
            var pm = _ctx.Get<PlayerManager>();

            Assert.Throws<Exception>(() => pm.Add(plr));
        }

        [Fact]
        public async Task PlayerManager_getByNickname_finds()
        {
            var plr = await LoginAsync(2503, "UniqueNickname");
            var pm = _ctx.Get<PlayerManager>();

            Assert.Same(plr, pm.GetByNickname("uniqueNICKNAME"));
            Assert.Null(pm.GetByNickname("doesnotexist"));
        }

        [Fact]
        public async Task PlayerManager_getMissing_returnsNull()
        {
            var pm = _ctx.Get<PlayerManager>();
            Assert.Null(pm.Get(999999));
        }
    }
}
