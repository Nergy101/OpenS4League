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
    /// Drives the GameRuleCommands (briefing/teamscore/gamestate/changehp) with a Developer player
    /// in a Practice room over the harness.
    /// </summary>
    public class GameRuleCommandTests
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

        private Room BuildRoom(Player plr)
        {
            var rm = _ctx.Get<RoomManager>();
            var channel = new Channel(new ChannelEntity { Id = 1, Name = "T", PlayerLimit = 32, Color = "FF0000", MinLevel = 0, MaxLevel = 99 }, rm);
            channel.Join(plr);
            var options = GameFixtures.CreateRoomCreationOptions();
            options.GameRule = GameRule.Practice;
            options.Map = 3;
            var (room, err) = channel.RoomManager.Create(options);
            Assert.Equal(RoomCreateError.OK, err);
            room.Join(plr);
            return room;
        }

        [Fact]
        public async Task BriefingCommand_broadcasts()
        {
            var dev = await LoginAsync(1501, SecurityLevel.Developer);
            BuildRoom(dev);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(dev, new[] { "briefing" });
            Assert.True(result);
        }

        [Fact]
        public async Task BriefingCommand_noRoom_returnsError()
        {
            var dev = await LoginAsync(1502, SecurityLevel.Developer);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(dev, new[] { "briefing" });
            Assert.False(result); // not in a room
        }

        [Fact]
        public async Task TeamScoreCommand_setsScore()
        {
            var dev = await LoginAsync(1503, SecurityLevel.Developer);
            var room = BuildRoom(dev);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var teamId = (int)dev.Team.Id;
            var result = await cmd.Execute(dev, new[] { "teamscore", teamId.ToString(), "5" });
            Assert.True(result);
            Assert.Equal(5u, dev.Team.Score);
        }

        [Fact]
        public async Task GameStateCommand_startStartsGame()
        {
            var dev = await LoginAsync(1504, SecurityLevel.Developer);
            BuildRoom(dev);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(dev, new[] { "gamestate", "start" });
            Assert.True(result);
        }

        [Fact]
        public async Task GameStateCommand_invalidState_returnsError()
        {
            var dev = await LoginAsync(1505, SecurityLevel.Developer);
            BuildRoom(dev);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(dev, new[] { "gamestate", "bogus" });
            Assert.False(result);
        }

        [Fact]
        public async Task ChangeHPCommand_sendsToSelf()
        {
            var dev = await LoginAsync(1506, SecurityLevel.Developer);
            BuildRoom(dev);
            var cmd = _ctx.Get<CommandService>();
            await cmd.StartAsync(System.Threading.CancellationToken.None);

            var result = await cmd.Execute(dev, new[] { "changehp", "100" });
            Assert.True(result);
        }
    }
}
