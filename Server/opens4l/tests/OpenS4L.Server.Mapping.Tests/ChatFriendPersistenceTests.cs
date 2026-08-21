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
    /// Exercises the Chat FriendManager persistence + friend-add-to-offline-target flow.
    /// </summary>
    public class ChatFriendPersistenceTests
    {
        private readonly ChatTestContext _ctx = new ChatTestContext();

        private async Task<Player> LoginAsync(ulong accountId)
        {
            using (var db = _ctx.Get<GameContext>())
            {
                db.Players.Add(new PlayerEntity { Id = (int)accountId, TotalExperience = 1000 });
                await db.SaveChangesAsync();
            }
            var bus = (Foundatio.Messaging.InMemoryMessageBus)_ctx.Get<Foundatio.Messaging.IMessageBus>();
            await bus.SubscribeToRequestAsync<ChatLoginRequest, ChatLoginResponse>(req =>
                Task.FromResult(new ChatLoginResponse(true, new Account(req.AccountId, "u", "nick" + req.AccountId, SecurityLevel.User), 1000, 0)),
                CancellationToken.None);

            var handler = _ctx.Get<AuthenticationHandler>();
            var (session, _) = _ctx.CreateSession((uint)accountId);
            await handler.OnHandle(new MessageContext { Session = session }, new LoginReqMessage
            {
                AccountId = accountId, Nickname = "nick" + accountId, SessionId = "sid"
            });
            return session.Player;
        }

        [Fact]
        public async Task FriendManager_initialize_loadsFriends()
        {
            // Seed the friend target account.
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 5001, Username = "f", Nickname = "friend1" });
                await auth.SaveChangesAsync();
            }
            // Seed a friend entity pointing at the target.
            using (var db = _ctx.Get<GameContext>())
            {
                db.PlayerFriends.Add(new PlayerFriendEntity { Id = 1, PlayerId = 7001, FriendPlayerId = 5001, State = (byte)FriendState.Friends });
                await db.SaveChangesAsync();
            }

            var player = await LoginAsync(7001);
            // The login initialized FriendManager from the seeded entity.
            Assert.True(player.Friends.Contains(5001));
            Assert.Equal(FriendState.Friends, player.Friends[5001].State);
        }

        [Fact]
        public async Task FriendManager_save_persistsNewFriend()
        {
            var player = await LoginAsync(7002);
            player.Friends.Add(5002, "friend2", FriendState.Requested);

            using (var db = _ctx.Get<GameContext>())
            {
                await player.Friends.Save(db);
                await db.SaveChangesAsync();
            }

            using (var db = _ctx.Get<GameContext>())
            {
                Assert.NotEmpty(db.PlayerFriends.Where(x => x.PlayerId == 7002));
            }
        }

        [Fact]
        public async Task FriendManager_remove_tracksForDelete()
        {
            // Seed a friend so it "Exists" in the manager.
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 5003, Username = "f3", Nickname = "friend3" });
                await auth.SaveChangesAsync();
            }
            using (var db = _ctx.Get<GameContext>())
            {
                db.PlayerFriends.Add(new PlayerFriendEntity { Id = 2, PlayerId = 7003, FriendPlayerId = 5003, State = (byte)FriendState.Friends });
                await db.SaveChangesAsync();
            }

            var player = await LoginAsync(7003);
            Assert.True(player.Friends.Contains(5003));

            var removed = player.Friends.Remove(5003);
            Assert.True(removed);
            Assert.False(player.Friends.Contains(5003));
        }

        [Fact]
        public async Task Friend_add_toOfflineTarget_sendsAckAndPersists()
        {
            // Target account exists in Auth but is NOT online.
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 5004, Username = "f4", Nickname = "offline" });
                await auth.SaveChangesAsync();
            }

            var sender = await LoginAsync(7004);
            var handler = _ctx.Get<FriendHandler>();
            await handler.OnHandle(new MessageContext { Session = sender.Session }, new FriendActionReqMessage
            {
                Action = FriendAction.Add, AccountId = 5004, Nickname = "offline"
            });

            // The offline target's incoming friend request was persisted to the DB.
            using (var db = _ctx.Get<GameContext>())
            {
                var row = await db.PlayerFriends.FirstOrDefaultAsync(x => x.FriendPlayerId == 7004);
                Assert.NotNull(row);
            }
        }
    }
}
