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
    /// Exercises the Game TeamManager/Team domain and a started game's score path over the harness.
    /// </summary>
    public class GameTeamTests
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
        public async Task TeamManager_addRemoveAndJoin()
        {
            var plr = await LoginAsync(9001);
            var room = BuildRoom(plr);
            var tm = room.TeamManager;

            // Practice room has Alpha + Beta teams; the player joined one.
            Assert.Equal(2, tm.Count);
            Assert.NotNull(plr.Team);
            Assert.Contains(plr, plr.Team.Values);
        }

        [Fact]
        public async Task TeamManager_changeTeam()
        {
            var plr = await LoginAsync(9002);
            var room = BuildRoom(plr);
            var tm = room.TeamManager;

            var original = plr.Team.Id;
            var target = original == TeamId.Alpha ? TeamId.Beta : TeamId.Alpha;
            // Practice room: Alpha has 1 player slot, Beta has 0 (spectator-only) → changing to the
            // empty Beta team returns Full (no free player slot).
            var result = tm.ChangeTeam(plr, target);
            Assert.Equal(TeamChangeError.Full, result);
            Assert.Equal(original, plr.Team.Id);
        }

        [Fact]
        public async Task TeamManager_changeMode()
        {
            var plr = await LoginAsync(9003);
            var room = BuildRoom(plr);
            var tm = room.TeamManager;

            // Practice room: Alpha team has 0 spectator slots → switching to Spectate returns Full.
            var result = tm.ChangeMode(plr, PlayerGameMode.Spectate);
            Assert.Equal(TeamChangeModeError.Full, result);
            Assert.Equal(PlayerGameMode.Normal, plr.Mode);
        }

        [Fact]
        public async Task Room_getBriefing()
        {
            var plr = await LoginAsync(9101);
            var room = BuildRoom(plr);

            var briefing = room.GetBriefing();
            Assert.NotNull(briefing);
            Assert.Contains(briefing.Players, p => p.AccountId == plr.Account.Id);
            room.BroadcastBriefing();
        }

        [Fact]
        public async Task Room_changeMaster()
        {
            var plr1 = await LoginAsync(9102);
            var plr2 = await LoginAsync(9103);
            var room = BuildRoom(plr1);
            // The Practice room's Alpha team has 1 slot, Beta 0 → the second player cannot join.
            var joinErr = room.Join(plr2);
            Assert.Equal(RoomJoinError.RoomFull, joinErr);

            room.ChangeMaster(plr2);
            // plr2 never joined (RoomFull), so ChangeMaster is a no-op.
            Assert.Equal(plr1, room.Master);
        }

        [Fact]
        public async Task Team_changeTeam_sameTeam_returnsAlreadyInTeam()
        {
            var plr = await LoginAsync(9004);
            var room = BuildRoom(plr);
            var tm = room.TeamManager;

            var result = tm.ChangeTeam(plr, plr.Team.Id);
            Assert.Equal(TeamChangeError.AlreadyInTeam, result);
        }
    }
}
