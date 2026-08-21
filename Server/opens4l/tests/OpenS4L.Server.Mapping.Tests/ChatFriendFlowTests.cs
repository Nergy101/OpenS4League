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
    /// Drives the Friend accept/deny flows (both players online) over the harness.
    /// </summary>
    public class ChatFriendFlowTests
    {
        private readonly ChatTestContext _ctx = new ChatTestContext();

        private async Task<(Session session, FakeSocketChannel channel)> LoginAsync(ulong accountId)
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
            var (session, channel) = _ctx.CreateSession((uint)accountId);
            await handler.OnHandle(new MessageContext { Session = session }, new LoginReqMessage
            {
                AccountId = accountId, Nickname = "nick" + accountId, SessionId = "sid"
            });
            return (session, channel);
        }

        [Fact]
        public async Task Friend_addAndAccept_bothOnline()
        {
            var (requester, _) = await LoginAsync(1001);
            var (receiver, receiverCh) = await LoginAsync(1002);

            // requester sends a friend request to receiver. Seed the target account so the
            // FriendHandler can find it in AuthContext.
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 1002, Username = "u1002", Nickname = "nick1002" });
                await auth.SaveChangesAsync();
            }

            var handler = _ctx.Get<FriendHandler>();
            await handler.OnHandle(new MessageContext { Session = requester }, new FriendActionReqMessage
            {
                Action = FriendAction.Add, AccountId = 1002, Nickname = "nick1002"
            });

            // Both have the friend in "requested" / "incoming" state.
            Assert.True(requester.Player.Friends.Contains(1002));
            Assert.True(receiver.Player.Friends.Contains(1001));

            // receiver accepts.
            await handler.OnHandle(new MessageContext { Session = receiver }, new FriendActionReqMessage
            {
                Action = FriendAction.AcceptRequest, AccountId = 1001, Nickname = "nick1001"
            });

            Assert.Equal(FriendState.Friends, requester.Player.Friends[1002].State);
            Assert.Equal(FriendState.Friends, receiver.Player.Friends[1001].State);
            Assert.Contains(receiverCh.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is FriendActionAckMessage);
        }

        [Fact]
        public async Task Friend_remove_removesFromBoth()
        {
            var (requester, _) = await LoginAsync(2001);
            var (receiver, _) = await LoginAsync(2002);

            var handler = _ctx.Get<FriendHandler>();
            await handler.OnHandle(new MessageContext { Session = requester }, new FriendActionReqMessage
            {
                Action = FriendAction.Add, AccountId = 2002, Nickname = "nick2002"
            });
            await handler.OnHandle(new MessageContext { Session = requester }, new FriendActionReqMessage
            {
                Action = FriendAction.Remove, AccountId = 2002, Nickname = "nick2002"
            });

            Assert.False(requester.Player.Friends.Contains(2002));
            Assert.False(receiver.Player.Friends.Contains(2001));
        }

        [Fact]
        public async Task Friend_denyRequest_removesFromBoth()
        {
            var (requester, _) = await LoginAsync(3001);
            var (receiver, _) = await LoginAsync(3002);
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 3002, Username = "u3002", Nickname = "nick3002" });
                await auth.SaveChangesAsync();
            }

            var handler = _ctx.Get<FriendHandler>();
            // requester sends a friend request.
            await handler.OnHandle(new MessageContext { Session = requester }, new FriendActionReqMessage
            {
                Action = FriendAction.Add, AccountId = 3002, Nickname = "nick3002"
            });
            Assert.True(requester.Player.Friends.Contains(3002));

            // receiver denies the request → both sides lose the friend entry.
            await handler.OnHandle(new MessageContext { Session = receiver }, new FriendActionReqMessage
            {
                Action = FriendAction.DenyRequest, AccountId = 3001, Nickname = "nick3001"
            });
            Assert.False(receiver.Player.Friends.Contains(3001));
            Assert.False(requester.Player.Friends.Contains(3002));
        }
    }
}
