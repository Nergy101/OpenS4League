using System;
using System.Linq;
using System.Threading;
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
    /// Drives the ClanManager member-management persistence paths (Approve/Kick/Ban/Unban/
    /// ChangeRole/ChangeInfo/ChangeAnnouncement) against a REAL Postgres database via Testcontainers.
    /// These methods use EF ExecuteUpdateAsync/ExecuteDeleteAsync, which the InMemory harness cannot
    /// run — a real relational provider is required.
    /// </summary>
    public class GameClanPostgresTests : IAsyncLifetime
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

        [Fact]
        public async Task ClanManager_approve_kick_ban_role_change_persist()
        {
            await _ctx.Get<ClanManager>().StartAsync(CancellationToken.None);
            var owner = await LoginAsync(1301);
            var member = await LoginAsync(1302);
            var cm = _ctx.Get<ClanManager>();

            var (clan, err) = await cm.CreateClan(owner, "PgClan", "desc",
                ClubArea.Europe, ClubActivity.Fellowship, "q1", "q2", "q3", "q4", "q5");
            Assert.Equal(ClanCreateError.None, err);

            // Member joins (private clan → JoinRequested).
            Assert.Equal(ClubJoinResult.Registered, await clan.Join(member, "a1", "a2", "a3", "a4", "a5"));
            var memberEntry = clan.GetMember(member.Account.Id);
            Assert.Equal(ClubMemberState.JoinRequested, memberEntry.State);

            // Approve → Joined (uses ExecuteUpdateAsync).
            Assert.Equal(ClubCommandResult.Success, await clan.Approve(owner, member.Account.Id));
            Assert.Equal(ClubMemberState.Joined, memberEntry.State);

            // Change role (uses ExecuteUpdateAsync).
            await clan.ChangeRole(memberEntry, ClubRole.Staff);
            Assert.Equal(ClubRole.Staff, memberEntry.Role);

            // Change announcement / info / join condition (use ExecuteUpdateAsync).
            await clan.ChangeAnnouncement("Welcome!");
            Assert.Equal("Welcome!", clan.Announcement);
            await clan.ChangeInfo(ClubArea.France, ClubActivity.ClanBattle, "new desc");
            Assert.Equal(ClubArea.France, clan.Area);
            await clan.ChangeJoinCondition(true, 0, "n1", "n2", "n3", "n4", "n5");
            Assert.True(clan.IsPublic);

            // Kick (uses ExecuteDeleteAsync).
            Assert.Equal(ClubCommandResult.Success, await clan.Kick(owner, member.Account.Id));
            Assert.Null(clan.GetMember(member.Account.Id));

            // Member rejoins (clan is now public → Joined) and is banned.
            Assert.Equal(ClubJoinResult.Joined, await clan.Join(member, "a1", "a2", "a3", "a4", "a5"));
            Assert.Equal(ClubCommandResult.Success, await clan.Ban(owner, member.Account.Id));
            Assert.Contains(member.Account.Id, clan.Bans);

            // Unban (uses ExecuteDeleteAsync).
            Assert.Equal(ClubCommandResult.Success, await clan.Unban(owner, member.Account.Id));
            Assert.DoesNotContain(member.Account.Id, clan.Bans);
        }

        [Fact]
        public async Task ClanManager_approve_nonexistentMember_returnsNotFound()
        {
            await _ctx.Get<ClanManager>().StartAsync(CancellationToken.None);
            var owner = await LoginAsync(1303);
            var cm = _ctx.Get<ClanManager>();
            var (clan, _) = await cm.CreateClan(owner, "PgClan2", "desc",
                ClubArea.Europe, ClubActivity.Fellowship, "q1", "q2", "q3", "q4", "q5");

            Assert.Equal(ClubCommandResult.MemberNotFound, await clan.Approve(owner, 999999UL));
            Assert.Equal(ClubCommandResult.MemberNotFound, await clan.Kick(owner, 999999UL));
            Assert.Equal(ClubCommandResult.MemberNotFound, await clan.Ban(owner, 999999UL));
            Assert.Equal(ClubCommandResult.MemberNotFound, await clan.Unban(owner, 999999UL));
        }
    }
}
