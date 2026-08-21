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
using OpenS4L.Server.Game.Services;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Exercises the Game admin/game-master Commands over the harness via CommandService.Execute.
    /// </summary>
    public class GameCommandTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();

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
        public async Task GMCommand_togglesMode()
        {
            var gm = await LoginAsync(8001, SecurityLevel.GameMaster);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(gm, new[] { "gm" });
            Assert.True(result);
            Assert.True(gm.IsInGMMode);

            result = await cmd.Execute(gm, new[] { "gm" });
            Assert.True(result);
            Assert.False(gm.IsInGMMode);
        }

        [Fact]
        public async Task AnnounceCommand_broadcasts()
        {
            var gm = await LoginAsync(8002, SecurityLevel.GameMaster);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(gm, new[] { "announce", "hello", "world" });
            Assert.True(result);
        }

        [Fact]
        public async Task KickCommand_kicksPlayer()
        {
            var gm = await LoginAsync(8003, SecurityLevel.GameMaster);
            var victim = await LoginAsync(8004, SecurityLevel.User);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(gm, new[] { "kick", "8004" });
            Assert.True(result);
        }

        [Fact]
        public async Task CommandService_userLacksPermission_returnsFalse()
        {
            var user = await LoginAsync(8005, SecurityLevel.User);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(user, new[] { "gm" });
            Assert.False(result); // user lacks GameMaster permission
        }

        [Fact]
        public async Task CommandService_unknownCommand_returnsFalse()
        {
            var gm = await LoginAsync(8006, SecurityLevel.GameMaster);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(gm, new[] { "nonexistentcommand" });
            Assert.False(result);
        }

        [Fact]
        public async Task CreateAccountCommand_createsAccount()
        {
            var admin = await LoginAsync(8007, SecurityLevel.Administrator);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(admin, new[] { "createaccount", "newuser", "secret" });
            Assert.True(result);

            using (var db = _ctx.Get<AuthContext>())
            {
                Assert.NotNull(await db.Accounts.FirstOrDefaultAsync(x => x.Username == "newuser"));
            }
        }

        [Fact]
        public async Task CreateAccountCommand_wrongArgs_returnsFalse()
        {
            var admin = await LoginAsync(8008, SecurityLevel.Administrator);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(admin, new[] { "createaccount", "onlyone" });
            Assert.False(result);
        }
    }
}
