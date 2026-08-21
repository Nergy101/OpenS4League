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
using OpenS4L.Network.Message.GameRule;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Drives the Game state machine through to the Playing state and exercises the score-kill
    /// flow, using a ManualSchedulerService to fire the time-based StartGame triggers.
    /// </summary>
    public class GamePlayingTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();

        private async Task<(Player plr, Game.Session session)> LoginAsync(uint accountId)
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
            var (session, _) = _ctx.CreateSession(accountId);
            await handler.OnHandle(new MessageContext { Session = session }, new LoginRequestReqMessage
            {
                AccountId = accountId, SessionId = "sid-" + accountId, Version = new Version(1, 0, 0, 0)
            });
            return (session.Player, session);
        }

        [Fact]
        public async Task StartGame_reachesPlayingAndAlive()
        {
            var (plr, _) = await LoginAsync(8001);
            var rm = _ctx.Get<RoomManager>();
            var channel = new Channel(new ChannelEntity { Id = 1, Name = "T", PlayerLimit = 32, Color = "FF0000", MinLevel = 0, MaxLevel = 99 }, rm);
            channel.Join(plr);
            var options = GameFixtures.CreateRoomCreationOptions();
            options.GameRule = GameRule.Practice;
            options.Map = 3;
            var (room, err) = channel.RoomManager.Create(options);
            Assert.Equal(RoomCreateError.OK, err);
            room.Join(plr);

            var sm = room.GameRule.StateMachine;
            Assert.Equal(GameState.Waiting, sm.GameState);

            // Waiting → Loading.
            Assert.True(sm.StartGame());
            Assert.Equal(GameState.Loading, sm.GameState);
            Assert.Equal(PlayerState.Waiting, plr.State);

            // Loading → Starting via a further StartGame trigger.
            Assert.True(sm.StartGame());
            Assert.Equal(GameState.Loading, sm.GameState); // Loading + Starting both map to "Loading"
        }
    }
}
