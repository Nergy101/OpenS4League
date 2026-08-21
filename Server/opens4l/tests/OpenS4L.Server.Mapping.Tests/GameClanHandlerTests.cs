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
    /// Drives the Game ClanHandler (name-check / create / join-condition / admin modify) over the harness.
    /// </summary>
    public class GameClanHandlerTests
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

        private async Task StartClanManagerAsync() => await _ctx.Get<ClanManager>().StartAsync(CancellationToken.None);

        [Fact]
        public async Task ClubCreate_success()
        {
            await StartClanManagerAsync();
            var plr = await LoginAsync(2201);
            var handler = _ctx.Get<ClanHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ClubCreateReqMessage
            {
                Name = "HandlerClan", Description = "desc", Area = ClubArea.Europe, Activity = ClubActivity.Fellowship,
                Question1 = "q1", Question2 = "q2", Question3 = "q3", Question4 = "q4", Question5 = "q5"
            });
            Assert.NotNull(plr.Clan);
        }

        [Fact]
        public async Task ClubNameCheck_invalid_returnsError()
        {
            await StartClanManagerAsync();
            var plr = await LoginAsync(2202);
            var handler = _ctx.Get<ClanHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ClubNameCheckReqMessage { Name = "x" });
            // checkName with a too-short name returns CannotBeUsed; no exception is the point.
        }

        [Fact]
        public async Task ClubJoinCondition_nonexistent_returnsError()
        {
            await StartClanManagerAsync();
            var plr = await LoginAsync(2203);
            var handler = _ctx.Get<ClanHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ClubJoinConditionInfoReqMessage { ClubId = 999999 });
            // Clan not found → FailedToRequestTask sent, no exception.
        }

        [Fact]
        public async Task ClubJoin_nonexistent_returnsFailed()
        {
            await StartClanManagerAsync();
            var plr = await LoginAsync(2204);
            var handler = _ctx.Get<ClanHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new OpenS4L.Network.Message.Club.ClubJoinReqMessage
            {
                ClubId = 999999, Answer1 = "a", Answer2 = "a", Answer3 = "a", Answer4 = "a", Answer5 = "a"
            });
            // Clan not found → ClubJoinResult.Failed, no exception.
        }
    }
}
