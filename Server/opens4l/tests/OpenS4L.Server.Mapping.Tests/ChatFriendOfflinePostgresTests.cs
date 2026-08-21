using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Common.Messaging;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Data.Chat;
using OpenS4L.Network.Message.Chat;
using OpenS4L.Server.Chat;
using OpenS4L.Server.Chat.Handlers;
using ProudNet;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Drives the Chat FriendHandler accept/deny flows against an OFFLINE requester — the branches
    /// that write via EF ExecuteUpdateAsync/ExecuteDeleteAsync, which require a real relational
    /// provider (Postgres via Testcontainers).
    /// </summary>
    public class ChatFriendOfflinePostgresTests : IAsyncLifetime
    {
        private PostgresDatabase _db;
        private ChatTestContext _ctx;

        public async Task InitializeAsync()
        {
            _db = await PostgresFixture.Instance.CreateDatabaseAsync();
            _ctx = new ChatTestContext(_db);
        }

        public async Task DisposeAsync()
        {
            _ctx?.Dispose();
            if (_db != null) await _db.DisposeAsync();
        }

        private async Task<(Session session, FakeSocketChannel channel)> LoginAsync(ulong accountId)
        {
            using (var db = _ctx.Get<GameContext>())
            {
                if (!await db.Players.AnyAsync(x => x.Id == (int)accountId))
                    db.Players.Add(new PlayerEntity { Id = (int)accountId, TotalExperience = 1000 });
                await db.SaveChangesAsync();
            }
            var bus = (Foundatio.Messaging.InMemoryMessageBus)_ctx.Get<Foundatio.Messaging.IMessageBus>();
            await bus.SubscribeToRequestAsync<ChatLoginRequest, ChatLoginResponse>(req =>
                Task.FromResult(new ChatLoginResponse(true, new Account(req.AccountId, "u", "nick" + req.AccountId, SecurityLevel.User), 1000, 0)),
                CancellationToken.None);

            var handler = _ctx.Get<AuthenticationHandler>();
            var (session, channel) = _ctx.CreateSession((uint)accountId);
            await handler.OnHandle(new MessageContext { Session = session }, new LoginReqMessage
            {
                AccountId = accountId, Nickname = "nick" + accountId, SessionId = "sid"
            });
            return (session, channel);
        }

        [Fact]
        public async Task Friend_accept_offlineRequester_updatesStateInDb()
        {
            // Seed the offline requester account + a friend row (requested) owned by the online player.
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 9101, Username = "r", Nickname = "nick9101" });
                await auth.SaveChangesAsync();
            }
            using (var db = _ctx.Get<GameContext>())
            {
                // Both the offline requester and the owner must exist as player rows (FK targets);
                // LoginAsync won't re-add the owner's row (idempotent).
                db.Players.Add(new PlayerEntity { Id = 9101, TotalExperience = 0 });
                db.Players.Add(new PlayerEntity { Id = 9102, TotalExperience = 1000 });
                // The online player's outgoing row (loads into their FriendManager on login) and the
                // requester's inbound row (which the offline accept updates).
                db.PlayerFriends.Add(new PlayerFriendEntity { Id = 1, PlayerId = 9102, FriendPlayerId = 9101, State = (byte)FriendState.Requested });
                db.PlayerFriends.Add(new PlayerFriendEntity { Id = 2, PlayerId = 9101, FriendPlayerId = 9102, State = (byte)FriendState.IncomingRequest });
                await db.SaveChangesAsync();
            }

            // The online player (9102) logs in; the friend row is loaded into their FriendManager.
            var (online, _) = await LoginAsync(9102);
            Assert.True(online.Player.Friends.Contains(9101));

            // Online player accepts the request; requester is offline → ExecuteUpdateAsync path.
            var handler = _ctx.Get<FriendHandler>();
            await handler.OnHandle(new MessageContext { Session = online }, new FriendActionReqMessage
            {
                Action = FriendAction.AcceptRequest, AccountId = 9101, Nickname = "nick9101"
            });

            // The requester's inbound DB row's state was updated to Friends (ExecuteUpdateAsync).
            using (var db = _ctx.Get<GameContext>())
            {
                var row = await db.PlayerFriends.SingleAsync(x => x.Id == 2);
                Assert.Equal((byte)FriendState.Friends, row.State);
            }
        }

        [Fact]
        public async Task Friend_deny_offlineRequester_deletesRowInDb()
        {
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 9201, Username = "r2", Nickname = "nick9201" });
                await auth.SaveChangesAsync();
            }
            using (var db = _ctx.Get<GameContext>())
            {
                // Both the offline requester and the owner must exist as player rows (FK targets);
                // LoginAsync won't re-add the owner's row (idempotent).
                db.Players.Add(new PlayerEntity { Id = 9201, TotalExperience = 0 });
                db.Players.Add(new PlayerEntity { Id = 9202, TotalExperience = 1000 });
                // The online player's outgoing row + the requester's inbound row (deleted by deny).
                db.PlayerFriends.Add(new PlayerFriendEntity { Id = 3, PlayerId = 9202, FriendPlayerId = 9201, State = (byte)FriendState.Requested });
                db.PlayerFriends.Add(new PlayerFriendEntity { Id = 4, PlayerId = 9201, FriendPlayerId = 9202, State = (byte)FriendState.IncomingRequest });
                await db.SaveChangesAsync();
            }

            var (online, _) = await LoginAsync(9202);
            Assert.True(online.Player.Friends.Contains(9201));

            var handler = _ctx.Get<FriendHandler>();
            await handler.OnHandle(new MessageContext { Session = online }, new FriendActionReqMessage
            {
                Action = FriendAction.DenyRequest, AccountId = 9201, Nickname = "nick9201"
            });

            // The requester's inbound row was deleted via ExecuteDeleteAsync.
            using (var db = _ctx.Get<GameContext>())
            {
                Assert.False(await db.PlayerFriends.AnyAsync(x => x.Id == 4));
            }
        }
    }
}
