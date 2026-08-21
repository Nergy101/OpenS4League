using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Common.Messaging;
using OpenS4L.Database;
using OpenS4L.Database.Game;
using OpenS4L.Network.Message.Chat;
using OpenS4L.Server.Chat;
using OpenS4L.Server.Chat.Handlers;
using OpenS4L.Server.Chat.Rules;
using ProudNet;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Covers the Channel/ChannelManager lifecycle, the firewall Rules, and SessionFactory.
    /// </summary>
    public class ChatChannelAndRulesTests
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
        public async Task Channel_joinAndLeave()
        {
            var (session, channel) = await LoginAsync(9001);
            var cm = _ctx.Get<ChannelManager>();
            var ch = cm.GetOrCreateChannel(1);
            Assert.NotNull(ch);

            ch.Join(session.Player);
            Assert.Single(ch.Players);
            Assert.Equal(ch, session.Player.Channel);

            ch.Leave(session.Player);
            Assert.Empty(ch.Players);
            Assert.Null(session.Player.Channel);
        }

        [Fact]
        public void ChannelManager_getOrCreate_isCached()
        {
            var cm = _ctx.Get<ChannelManager>();
            var a = cm.GetOrCreateChannel(2);
            var b = cm.GetOrCreateChannel(2);
            Assert.Same(a, b);
            Assert.Equal(1, cm.Count);
            Assert.Equal(a, cm[2]);
        }

        [Fact]
        public async Task Channel_sendChatMessage_broadcasts()
        {
            var (session, channel) = await LoginAsync(9002);
            var cm = _ctx.Get<ChannelManager>();
            var ch = cm.GetOrCreateChannel(3);
            ch.Join(session.Player);

            ch.SendChatMessage(session.Player, "hello");
            var sent = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<MessageChatAckMessage>().Any();
            Assert.True(sent);
        }

        [Fact]
        public async Task MustBeLoggedIn_allowsLoggedInPlayer()
        {
            var (session, _) = await LoginAsync(9003);
            var rule = new MustBeLoggedIn();
            var allowed = await rule.IsMessageAllowed(new MessageContext { Session = session }, new MessageChatReqMessage());
            Assert.True(allowed);
        }

        [Fact]
        public void MustBeLoggedIn_deniesNullPlayer()
        {
            var session = _ctx.CreateSession(123).session;
            session.Player = null;
            var rule = new MustBeLoggedIn();
            var allowed = rule.IsMessageAllowed(new MessageContext { Session = session }, new object()).GetAwaiter().GetResult();
            Assert.False(allowed);
        }

        [Fact]
        public void SessionFactory_createsRealSession()
        {
            var factory = new SessionFactory();
            var channel = new FakeSocketChannel(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 21000));
            var session = factory.Create(new Logging.Logger<ProudSession>(), 42, channel);
            Assert.IsType<Session>(session);
            Assert.Equal(42u, session.HostId);
        }
    }
}
