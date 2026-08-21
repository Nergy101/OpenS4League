using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Message.Club;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Exercises the Game ClanManager over the harness: name checking and clan creation.
    /// </summary>
    public class GameClanTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();

        private async Task<Player> LoginAsync(uint accountId)
        {
            // ClanManager._clans is initialized by the hosted service's StartAsync.
            await _ctx.Get<ClanManager>().StartAsync(System.Threading.CancellationToken.None);

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

        [Fact]
        public async Task ClanManager_checkName()
        {
            var plr = await LoginAsync(7001);
            var cm = _ctx.Get<ClanManager>();

            Assert.Equal(ClubNameCheckResult.CannotBeUsed, cm.CheckClanName(""));
            Assert.Equal(ClubNameCheckResult.TooShort, cm.CheckClanName("ab"));
            Assert.Equal(ClubNameCheckResult.Available, cm.CheckClanName("MyClan"));
        }

        [Fact]
        public async Task ClanManager_createClan()
        {
            var plr = await LoginAsync(7002);
            var cm = _ctx.Get<ClanManager>();

            var (clan, err) = await cm.CreateClan(plr, "MyClan", "desc",
                ClubArea.Europe, ClubActivity.Fellowship,
                "q1", "q2", "q3", "q4", "q5");
            Assert.Equal(ClanCreateError.None, err);
            Assert.NotNull(clan);
            Assert.Equal(plr.Account.Id, clan.Owner.AccountId);
            Assert.Same(clan, plr.Clan);
        }

        [Fact]
        public async Task ClanManager_createClan_duplicateName_returnsError()
        {
            var plr = await LoginAsync(7003);
            var plr2 = await LoginAsync(7004);
            var cm = _ctx.Get<ClanManager>();
            await cm.CreateClan(plr, "MyClan2", "desc", ClubArea.Europe, ClubActivity.Fellowship, "q1", "q2", "q3", "q4", "q5");

            // A second player trying to create a clan with the same name fails.
            var (_, err) = await cm.CreateClan(plr2, "MyClan2", "desc", ClubArea.Europe, ClubActivity.Fellowship, "q1", "q2", "q3", "q4", "q5");
            Assert.Equal(ClanCreateError.NameAlreadyExists, err);
        }

        [Fact]
        public async Task ClanManager_joinAndLeave()
        {
            var owner = await LoginAsync(7101);
            var member = await LoginAsync(7102);
            var cm = _ctx.Get<ClanManager>();

            var (clan, err) = await cm.CreateClan(owner, "JoinClan", "desc",
                ClubArea.Europe, ClubActivity.Fellowship, "q1", "q2", "q3", "q4", "q5");
            Assert.Equal(ClanCreateError.None, err);

            // Owner is the single member (can't leave a 1-member clan).
            Assert.False(await clan.Leave(owner));

            // A second player joins (clan is private by default → join-requested).
            var joinResult = await clan.Join(member, "a1", "a2", "a3", "a4", "a5");
            Assert.Equal(ClubJoinResult.Registered, joinResult);
            Assert.Equal(clan, member.Clan);

            // Now the member can leave.
            Assert.True(await clan.Leave(member));
            Assert.Null(member.Clan);
        }

        [Fact]
        public async Task ClanManager_memberJoinStates()
        {
            var owner = await LoginAsync(7301);
            var member = await LoginAsync(7302);
            var cm = _ctx.Get<ClanManager>();
            var (clan, _) = await cm.CreateClan(owner, "MgmtClan", "desc",
                ClubArea.Europe, ClubActivity.Fellowship, "q1", "q2", "q3", "q4", "q5");

            // Member joins the private clan → JoinRequested.
            Assert.Equal(ClubJoinResult.Registered, await clan.Join(member, "a1", "a2", "a3", "a4", "a5"));
            var memberEntry = clan.GetMember(member.Account.Id);
            Assert.Equal(ClubMemberState.JoinRequested, memberEntry.State);
            Assert.Equal(clan, member.Clan);

            // Already in a clan → AlreadyRegistered.
            Assert.Equal(ClubJoinResult.AlreadyRegistered, await clan.Join(member, "a1", "a2", "a3", "a4", "a5"));
        }

        [Fact]
        public async Task ClanManager_memberDecline_removes()
        {
            var owner = await LoginAsync(7303);
            var member = await LoginAsync(7304);
            var cm = _ctx.Get<ClanManager>();
            var (clan, _) = await cm.CreateClan(owner, "DeclineClan", "desc",
                ClubArea.Europe, ClubActivity.Fellowship, "q1", "q2", "q3", "q4", "q5");

            await clan.Join(member, "a1", "a2", "a3", "a4", "a5");
            Assert.Equal(ClubCommandResult.Success, await clan.Decline(owner, member.Account.Id));
            Assert.Null(clan.GetMember(member.Account.Id));
            Assert.Null(member.Clan);
        }

        [Fact]
        public async Task ClanManager_getClubInfo()
        {
            var owner = await LoginAsync(7201);
            var cm = _ctx.Get<ClanManager>();
            var (clan, _) = await cm.CreateClan(owner, "InfoClan", "desc",
                ClubArea.Europe, ClubActivity.Fellowship, "q1", "q2", "q3", "q4", "q5");

            var info = await clan.GetClubInfo();
            Assert.Equal(clan.Id, info.ClanId);
            Assert.Equal("InfoClan", info.ClanName);
            Assert.Equal(1, info.MemberCount);
        }
    }
}
