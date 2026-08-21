using System;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Messaging;
using Microsoft.EntityFrameworkCore;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Common.Messaging;
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
    /// Drives the Game PlayerSaveService + IpcService + ServerlistService hosted services over the harness.
    /// </summary>
    public class GameServiceTests
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
        public async Task PlayerSaveService_startAndFire()
        {
            var plr = await LoginAsync(2301);
            var svc = _ctx.Get<PlayerSaveService>();
            await svc.StartAsync(CancellationToken.None);

            // Fire the scheduled save via the manual scheduler.
            _ctx.Get<ManualSchedulerService>().RunNextScheduled();

            await svc.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task IpcService_startAndStop()
        {
            var ipc = _ctx.Get<IpcService>();
            await ipc.StartAsync(CancellationToken.None);
            await ipc.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ServerlistService_startAndStop()
        {
            var svc = _ctx.Get<ServerlistService>();
            await svc.StartAsync(CancellationToken.None);
            await svc.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task IpcService_chatLogin_responds()
        {
            var plr = await LoginAsync(2302);
            var ipc = _ctx.Get<IpcService>();
            await ipc.StartAsync(CancellationToken.None);

            var bus = _ctx.Get<Foundatio.Messaging.IMessageBus>();
            var resp = await bus.PublishRequestAsync<ChatLoginRequest, ChatLoginResponse>(
                new ChatLoginRequest(2302, plr.Session.SessionId));
            Assert.True(resp.OK);

            await ipc.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task IpcService_chatLogin_wrongSession_respondsFalse()
        {
            var plr = await LoginAsync(2303);
            var ipc = _ctx.Get<IpcService>();
            await ipc.StartAsync(CancellationToken.None);

            var bus = _ctx.Get<Foundatio.Messaging.IMessageBus>();
            var resp = await bus.PublishRequestAsync<ChatLoginRequest, ChatLoginResponse>(
                new ChatLoginRequest(2303, "wrong"));
            Assert.False(resp.OK);

            await ipc.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task IpcService_levelFromExperience_responds()
        {
            var ipc = _ctx.Get<IpcService>();
            await ipc.StartAsync(CancellationToken.None);

            var bus = _ctx.Get<Foundatio.Messaging.IMessageBus>();
            var resp = await bus.PublishRequestAsync<LevelFromExperienceRequest, LevelFromExperienceResponse>(
                new LevelFromExperienceRequest(1000));
            Assert.NotNull(resp);

            await ipc.StopAsync(CancellationToken.None);
        }
    }
}
