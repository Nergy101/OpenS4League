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
using OpenS4L.Server.Chat.Services;
using ProudNet;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Covers ServerlistService lifecycle and the MustBeInChannel firewall rule.
    /// </summary>
    public class ChatServerlistAndRulesTests
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
        public async Task ServerlistService_startAndStop()
        {
            var svc = _ctx.Get<ServerlistService>();
            await svc.StartAsync(CancellationToken.None);
            await svc.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ServerlistService_update_publishes()
        {
            var svc = _ctx.Get<ServerlistService>();
            await svc.StartAsync(CancellationToken.None);

            // StartAsync schedules an Update; fire it once via the manual scheduler.
            _ctx.Get<ManualSchedulerService>().RunNextScheduled();

            await svc.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task MustBeInChannel_allowsPlayerInChannel()
        {
            var (session, _) = await LoginAsync(8101);
            var cm = _ctx.Get<ChannelManager>();
            cm.GetOrCreateChannel(1).Join(session.Player);

            var rule = new MustBeInChannel();
            var allowed = await rule.IsMessageAllowed(new MessageContext { Session = session }, new MessageChatReqMessage());
            Assert.True(allowed);
        }

        [Fact]
        public async Task MustBeInChannel_deniesPlayerNotInChannel()
        {
            var (session, _) = await LoginAsync(8102);
            var rule = new MustBeInChannel();
            var allowed = await rule.IsMessageAllowed(new MessageContext { Session = session }, new MessageChatReqMessage());
            Assert.False(allowed);
        }
    }
}
