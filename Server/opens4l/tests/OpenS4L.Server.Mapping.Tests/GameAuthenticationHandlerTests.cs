using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
    /// Drives the Game server login flow over the harness: real Session, real handler, in-memory
    /// cache/EF. Requires a cached session key + seeded account.
    /// </summary>
    public class GameAuthenticationHandlerTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();
        private const uint AccountId = 9001;
        private const string Nick = "gamernick";

        private async Task<Game.Session> SeedAndLoginAsync()
        {
            // Configure the client version + cache the session key.
            var appOptions = _ctx.Get<Microsoft.Extensions.Options.IOptionsMonitor<AppOptions>>();
            // (configured via Configure<AppOptions> below — set ClientVersions in context)

            var cache = (Foundatio.Caching.InMemoryCacheClient)_ctx.Get<Foundatio.Caching.ICacheClient>();
            await cache.SetAsync<string>(Constants.Cache.SessionKey(AccountId), "sid-123");

            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity
                {
                    Id = (int)AccountId, Username = "gamer", Nickname = Nick, SecurityLevel = 1
                });
                await auth.SaveChangesAsync();
            }
            using (var db = _ctx.Get<GameContext>())
            {
                db.Players.Add(new PlayerEntity { Id = (int)AccountId, TotalExperience = 1000 });
                await db.SaveChangesAsync();
            }

            var handler = _ctx.Get<AuthenticationHandler>();
            var (session, _) = _ctx.CreateSession(AccountId);
            await handler.OnHandle(new MessageContext { Session = session }, new LoginRequestReqMessage
            {
                AccountId = AccountId,
                SessionId = "sid-123",
                Version = new Version(1, 0, 0, 0),
                KickConnection = false
            });
            return session;
        }

        [Fact]
        public async Task Login_success_initializesPlayer()
        {
            // Need a matching client version configured.
            var session = await SeedAndLoginAsync();
            Assert.NotNull(session.Player);
            Assert.Equal(AccountId, session.Player.Account.Id);
            Assert.True(_ctx.Get<PlayerManager>().Contains(AccountId));
        }
    }
}
