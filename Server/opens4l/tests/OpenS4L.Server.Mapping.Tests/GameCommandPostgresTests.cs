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
using OpenS4L.Server.Game.Services;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Drives the GameMaster ban/unban Commands against a real Postgres database (they persist to
    /// AuthContext with SaveChangesAsync, exercising the relational provider).
    /// </summary>
    public class GameCommandPostgresTests : IAsyncLifetime
    {
        private PostgresDatabase _db;
        private GameTestContext _ctx;

        public async Task InitializeAsync()
        {
            _db = await PostgresFixture.Instance.CreateDatabaseAsync();
            _ctx = new GameTestContext(_db);
        }

        public async Task DisposeAsync()
        {
            _ctx?.Dispose();
            if (_db != null) await _db.DisposeAsync();
        }

        private async Task<Player> LoginAsync(uint accountId, SecurityLevel level = SecurityLevel.User)
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
                if (!await db.Players.AnyAsync(x => x.Id == (int)accountId))
                    db.Players.Add(new PlayerEntity { Id = (int)accountId, TotalExperience = 1000 });
                await db.SaveChangesAsync();
            }

            var handler = _ctx.Get<AuthenticationHandler>();
            var (session, _) = _ctx.CreateSession(accountId);
            await handler.OnHandle(new MessageContext { Session = session }, new LoginRequestReqMessage
            {
                AccountId = accountId, SessionId = "sid-" + accountId, Version = new Version(1, 0, 0, 0)
            });
            return session.Player;
        }

        [Fact]
        public async Task BanCommand_bansThenUnbans()
        {
            var gm = await LoginAsync(1401, SecurityLevel.GameMaster);
            var target = await LoginAsync(1402, SecurityLevel.User);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            // Ban target 1402 for 1 day.
            var banResult = await cmd.Execute(gm, new[] { "ban", "1402", "1:0:0:0", "spam" });
            Assert.True(banResult);

            using (var db = _ctx.Get<AuthContext>())
            {
                var account = await db.Accounts.Include(x => x.Bans).SingleAsync(x => x.Id == 1402);
                Assert.NotEmpty(account.Bans);
                Assert.Contains("spam", account.Bans.First().Reason);
            }

            // Unban target 1402.
            var unbanResult = await cmd.Execute(gm, new[] { "unban", "1402" });
            Assert.True(unbanResult);

            using (var db = _ctx.Get<AuthContext>())
            {
                var account = await db.Accounts.Include(x => x.Bans).SingleAsync(x => x.Id == 1402);
                Assert.Empty(account.Bans);
            }
        }

        [Fact]
        public async Task BanCommand_invalidDuration_returnsError()
        {
            var gm = await LoginAsync(1403, SecurityLevel.GameMaster);
            var target = await LoginAsync(1404, SecurityLevel.User);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(gm, new[] { "ban", "1404", "notaduration", "spam" });
            Assert.True(result); // handles the bad duration and replies an error, still returns true

            using (var db = _ctx.Get<AuthContext>())
            {
                var account = await db.Accounts.Include(x => x.Bans).SingleAsync(x => x.Id == 1404);
                Assert.Empty(account.Bans); // no ban was created
            }
        }
    }
}
