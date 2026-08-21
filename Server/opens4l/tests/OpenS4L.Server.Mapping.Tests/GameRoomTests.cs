using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Data.GameRule;
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
    /// Drives the Game Room/RoomManager/Channel lifecycle over the harness.
    /// </summary>
    public class GameRoomTests
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
            var (session, _) = _ctx.CreateSession(accountId);
            await handler.OnHandle(new MessageContext { Session = session }, new LoginRequestReqMessage
            {
                AccountId = accountId, SessionId = "sid-" + accountId, Version = new Version(1, 0, 0, 0)
            });
            return session.Player;
        }

        private Channel BuildChannel()
        {
            var rm = _ctx.Get<RoomManager>();
            return new Channel(new ChannelEntity
            {
                Id = 1, Name = "Test", PlayerLimit = 32, Color = "FF0000",
                MinLevel = 0, MaxLevel = 99
            }, rm);
        }

        [Fact]
        public async Task Channel_joinAndLeave()
        {
            var plr = await LoginAsync(8001);
            var channel = BuildChannel();

            var err = channel.Join(plr);
            Assert.Equal(ChannelJoinError.OK, err);
            Assert.Equal(channel, plr.Channel);
            Assert.Contains(plr, channel.Players.Values);

            channel.Leave(plr);
            Assert.Null(plr.Channel);
        }

        [Fact]
        public async Task Channel_join_alreadyInChannel_returnsError()
        {
            var plr = await LoginAsync(8002);
            var channel = BuildChannel();
            channel.Join(plr);
            Assert.Equal(ChannelJoinError.AlreadyInChannel, channel.Join(plr));
        }

        [Fact]
        public async Task Room_createAndRemove()
        {
            var plr = await LoginAsync(8003);
            var channel = BuildChannel();
            channel.Join(plr);

            var options = GameFixtures.CreateRoomCreationOptions();
            options.GameRule = GameRule.Practice;
            options.Map = 3;

            var rm = channel.RoomManager;
            var (room, err) = rm.Create(options);
            Assert.Equal(RoomCreateError.OK, err);
            Assert.NotNull(room);
            Assert.Equal(rm.Channel, channel);
            Assert.Contains(room, rm);

            // Room is empty, so it can be removed.
            Assert.True(rm.Remove(room));
            Assert.DoesNotContain(room, rm);
        }

        [Fact]
        public async Task Room_joinAndLeave()
        {
            var plr = await LoginAsync(8005);
            var channel = BuildChannel();
            channel.Join(plr);

            var options = GameFixtures.CreateRoomCreationOptions();
            options.GameRule = GameRule.Practice;
            options.Map = 3;
            var (room, err) = channel.RoomManager.Create(options);
            Assert.Equal(RoomCreateError.OK, err);

            var joinErr = room.Join(plr);
            Assert.Equal(RoomJoinError.OK, joinErr);
            Assert.Equal(room, plr.Room);
            Assert.Equal(room.Master, plr);
            Assert.Contains(plr, room.Players.Values);

            room.Leave(plr);
            Assert.Null(plr.Room);
            Assert.Empty(room.Players);
        }

        [Fact]
        public async Task Room_join_secondTime_returnsAlreadyInRoom()
        {
            var plr = await LoginAsync(8006);
            var channel = BuildChannel();
            channel.Join(plr);

            var options = GameFixtures.CreateRoomCreationOptions();
            options.GameRule = GameRule.Practice;
            options.Map = 3;
            var (room, _) = channel.RoomManager.Create(options);

            Assert.Equal(RoomJoinError.OK, room.Join(plr));
            Assert.Equal(RoomJoinError.AlreadyInRoom, room.Join(plr));
        }

        [Fact]
        public async Task Room_changeRules()
        {
            var plr = await LoginAsync(8007);
            var channel = BuildChannel();
            channel.Join(plr);

            var options = GameFixtures.CreateRoomCreationOptions();
            options.GameRule = GameRule.Practice;
            options.Map = 3;
            var (room, _) = channel.RoomManager.Create(options);

            var result = room.ChangeRules(new ChangeRule2Dto
            {
                GameRule = GameRule.Practice,
                Map = 3,
                PlayerLimit = 4,
                SpectatorLimit = 2,
                TimeLimit = TimeSpan.FromMinutes(10),
                ScoreLimit = 30,
                Password = "",
                Name = "Renamed",
                Settings = RoomSettings.IsFriendly
            });
            Assert.Equal(RoomChangeRulesError.OK, result);
            Assert.Equal("Renamed", room.Options.Name);
        }

        [Fact]
        public async Task Room_changeRules_invalidMap_returnsError()
        {
            var plr = await LoginAsync(8008);
            var channel = BuildChannel();
            channel.Join(plr);

            var options = GameFixtures.CreateRoomCreationOptions();
            options.GameRule = GameRule.Practice;
            options.Map = 3;
            var (room, _) = channel.RoomManager.Create(options);

            var result = room.ChangeRules(new ChangeRule2Dto
            {
                GameRule = GameRule.Practice,
                Map = (byte)99, // not in fixture
                PlayerLimit = 4,
                SpectatorLimit = 2,
                TimeLimit = TimeSpan.FromMinutes(10),
                ScoreLimit = 30,
                Password = "",
                Name = "Renamed",
                Settings = RoomSettings.IsFriendly
            });
            Assert.Equal(RoomChangeRulesError.InvalidMap, result);
        }

        [Fact]
        public async Task Room_startGame_transitions()
        {
            var plr = await LoginAsync(8009);
            var channel = BuildChannel();
            channel.Join(plr);

            var options = GameFixtures.CreateRoomCreationOptions();
            options.GameRule = GameRule.Practice;
            options.Map = 3;
            var (room, _) = channel.RoomManager.Create(options);
            room.Join(plr);

            // Practice can start from the Waiting state with the room's players.
            var started = room.GameRule.StateMachine.StartGame();
            Assert.True(started);
            Assert.NotEqual(GameState.Waiting, room.GameRule.StateMachine.GameState);
        }

        [Fact]
        public async Task Room_create_invalidMap_returnsError()
        {
            var plr = await LoginAsync(8004);
            var channel = BuildChannel();

            var options = GameFixtures.CreateRoomCreationOptions();
            options.GameRule = GameRule.Practice;
            options.Map = (byte)99; // not in the fixture

            var rm = channel.RoomManager;
            var (_, err) = rm.Create(options);
            Assert.Equal(RoomCreateError.InvalidMap, err);
        }
    }
}
