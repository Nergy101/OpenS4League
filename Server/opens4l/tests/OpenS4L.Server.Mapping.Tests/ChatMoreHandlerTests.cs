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
    /// Drives the Deny, UserData, Chat, and Friend handlers end-to-end over the fake-transport
    /// harness. Uses a login helper that runs the real AuthenticationHandler so the session has a
    /// fully-initialized Player.
    /// </summary>
    public class ChatMoreHandlerTests
    {
        private readonly ChatTestContext _ctx = new ChatTestContext();

        private async Task<(Session session, FakeSocketChannel channel)> LoginAsync(ulong accountId, string nick)
        {
            using (var db = _ctx.Get<GameContext>())
            {
                db.Players.Add(new PlayerEntity { Id = (int)accountId, TotalExperience = 1000 });
                await db.SaveChangesAsync();
            }

            var bus = (Foundatio.Messaging.InMemoryMessageBus)_ctx.Get<Foundatio.Messaging.IMessageBus>();
            // One responder that returns a matching account for ANY login request, so multiple
            // players can log in against the same bus.
            await bus.SubscribeToRequestAsync<ChatLoginRequest, ChatLoginResponse>(req =>
            {
                var acc = new Account(req.AccountId, "u", "nick" + req.AccountId, SecurityLevel.User);
                return Task.FromResult(new ChatLoginResponse(true, acc, 1000, 0));
            }, CancellationToken.None);

            var handler = _ctx.Get<AuthenticationHandler>();
            var (session, channel) = _ctx.CreateSession((uint)accountId);
            await handler.OnHandle(new MessageContext { Session = session }, new LoginReqMessage
            {
                AccountId = accountId, Nickname = "nick" + accountId, SessionId = "sid"
            });
            return (session, channel);
        }

        [Fact]
        public async Task Chat_whisper_toOnlinePlayer()
        {
            var (sender, _) = await LoginAsync(8201, "sender");
            var (recipient, recipientCh) = await LoginAsync(8202, "recipient");

            var handler = _ctx.Get<ChatHandler>();
            await handler.OnHandle(Ctx(sender), new MessageWhisperChatReqMessage
            {
                ToNickname = "nick8202",
                Message = "hi there"
            });

            // The recipient received the whisper.
            Assert.Contains(recipientCh.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is MessageWhisperChatAckMessage);
        }

        [Fact]
        public async Task Chat_whisper_toOfflinePlayer_returnsSystemMessage()
        {
            var (sender, channel) = await LoginAsync(8203, "sender");

            var handler = _ctx.Get<ChatHandler>();
            await handler.OnHandle(Ctx(sender), new MessageWhisperChatReqMessage
            {
                ToNickname = "ghost",
                Message = "hi"
            });

            // The sender gets a "not online" system message.
            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<MessageChatAckMessage>().FirstOrDefault();
            Assert.NotNull(ack);
            Assert.Contains("not online", ack.Message);
        }

        [Fact]
        public async Task Chat_whisper_toIgnoringPlayer_returnsSystemMessage()
        {
            var (sender, _) = await LoginAsync(8204, "sender");
            var (recipient, _) = await LoginAsync(8205, "recipient");
            var senderChannel = _ctx.Get<PlayerManager>()[8204].Session;

            // recipient ignores sender.
            _ctx.Get<PlayerManager>()[8205].Ignore.Add(sender.Player.Account.Id, "sender");

            var handler = _ctx.Get<ChatHandler>();
            await handler.OnHandle(Ctx(sender), new MessageWhisperChatReqMessage
            {
                ToNickname = "nick8205",
                Message = "hi"
            });
        }

        private static MessageContext Ctx(Session s) => new MessageContext { Session = s };

        private static T FirstSent<T>(FakeSocketChannel channel) where T : class
        {
            return channel.Outbound
                .Select(o => o.GetType().GetProperty("Message")?.GetValue(o))
                .OfType<T>()
                .FirstOrDefault();
        }

        [Fact]
        public async Task Deny_add_sendsAck()
        {
            // Two players logged in.
            var (_, _) = await LoginAsync(5001, "victim");
            var (plr, channel) = await LoginAsync(5002, "me");
            var victim = _ctx.Get<PlayerManager>()[5001];

            var handler = _ctx.Get<DenyHandler>();
            await handler.OnHandle(Ctx(plr), new DenyActionReqMessage
            {
                Action = DenyAction.Add,
                Deny = new DenyDto { AccountId = 5001, Nickname = "victim" }
            });

            var ack = FirstSent<DenyActionAckMessage>(channel);
            Assert.NotNull(ack);
            Assert.Equal(DenyAction.Add, ack.Action);
            Assert.True(plr.Player.Ignore.Contains(5001));
        }

        [Fact]
        public async Task Deny_remove_sendsAck()
        {
            var (_, _) = await LoginAsync(6001, "victim");
            var (plr, channel) = await LoginAsync(6002, "me");
            var victim = _ctx.Get<PlayerManager>()[6001];

            // Pre-add to the ignore list.
            plr.Player.Ignore.Add(victim.Account.Id, victim.Account.Nickname);

            var handler = _ctx.Get<DenyHandler>();
            await handler.OnHandle(Ctx(plr), new DenyActionReqMessage
            {
                Action = DenyAction.Remove,
                Deny = new DenyDto { AccountId = 6001, Nickname = "victim" }
            });

            var ack = FirstSent<DenyActionAckMessage>(channel);
            Assert.NotNull(ack);
            Assert.Equal(DenyAction.Remove, ack.Action);
            Assert.False(plr.Player.Ignore.Contains(6001));
        }

        [Fact]
        public async Task UserData_ownAccount_sendsOwnData()
        {
            var (plr, channel) = await LoginAsync(7001, "self");
            var handler = _ctx.Get<UserDataHandler>();

            await handler.OnHandle(Ctx(plr), new UserDataOneReqMessage { AccountId = 7001 });

            var ack = FirstSent<UserDataFourAckMessage>(channel);
            Assert.NotNull(ack);
            Assert.NotNull(ack.UserData);
        }

        [Fact]
        public async Task Chat_channelMessage_broadcastsToChannelMembers()
        {
            var (plr, _) = await LoginAsync(8001, "speaker");
            var handler = _ctx.Get<ChatHandler>();

            var result = await handler.OnHandle(Ctx(plr), new MessageChatReqMessage
            {
                ChatType = ChatType.Channel,
                Message = "hello"
            });

            Assert.True(result);
        }

        [Fact]
        public async Task Friend_add_sendsAck()
        {
            var (target, _) = await LoginAsync(9001, "target");
            var (plr, channel) = await LoginAsync(9002, "me");

            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 9001, Username = "target", Nickname = "target" });
                await auth.SaveChangesAsync();
            }

            var handler = _ctx.Get<FriendHandler>();
            await handler.OnHandle(Ctx(plr), new FriendActionReqMessage
            {
                Action = FriendAction.Add,
                AccountId = 9001,
                Nickname = "target"
            });

            var ack = FirstSent<FriendActionAckMessage>(channel);
            Assert.NotNull(ack);
            Assert.Equal(FriendActionResult.Success, ack.Result);
            Assert.True(plr.Player.Friends.Contains(9001));
        }

        [Fact]
        public async Task Friend_add_nonexistentUser_returnsError()
        {
            var (plr, channel) = await LoginAsync(9101, "me");
            var handler = _ctx.Get<FriendHandler>();

            await handler.OnHandle(Ctx(plr), new FriendActionReqMessage
            {
                Action = FriendAction.Add,
                AccountId = 99999,
                Nickname = "ghost"
            });

            var ack = FirstSent<FriendActionAckMessage>(channel);
            Assert.NotNull(ack);
            Assert.Equal(FriendActionResult.UserDoesNotExist, ack.Result);
        }
    }
}
