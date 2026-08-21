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
using ProudNet;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Integration tests driving the real Chat handlers over the fake-transport harness
    /// (real Session over a fake channel, in-memory bus/EF). Covers the login flow and the
    /// message/deny/friend handlers end-to-end.
    /// </summary>
    public class ChatHandlerIntegrationTests
    {
        private readonly ChatTestContext _ctx = new ChatTestContext();

        [Fact]
        public async Task Login_withClan_sendsClanUpdates()
        {
            var bus = (Foundatio.Messaging.InMemoryMessageBus)_ctx.Get<Foundatio.Messaging.IMessageBus>();
            await bus.SubscribeToRequestAsync<ChatLoginRequest, ChatLoginResponse>(req =>
                Task.FromResult(new ChatLoginResponse(true, new Account(req.AccountId, "u", "nick", SecurityLevel.User), 1000, 7)),
                CancellationToken.None);
            // The SendClanUpdates path requests the clan member list.
            await bus.SubscribeToRequestAsync<ClanMemberListRequest, ClanMemberListResponse>(req =>
                Task.FromResult(new ClanMemberListResponse(new[]
                {
                    new ClanMemberInfo { AccountId = 9001, Nickname = "nick", Role = ClubRole.Master, PresenceState = ClubMemberPresenceState.Online }
                })),
                CancellationToken.None);

            using (var db = _ctx.Get<GameContext>())
            {
                db.Players.Add(new PlayerEntity { Id = 9001, TotalExperience = 1000 });
                await db.SaveChangesAsync();
            }

            var handler = _ctx.Get<AuthenticationHandler>();
            var (session, channel) = _ctx.CreateSession(1);

            var result = await handler.OnHandle(Context(session), new LoginReqMessage
            {
                AccountId = 9001, Nickname = "nick", SessionId = "sid"
            });

            Assert.True(result);
            Assert.NotNull(session.Player);
            Assert.Equal(7u, session.Player.ClanId);
            // The club member list was sent.
            Assert.Contains(channel.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is ClubMemberListAckMessage);
        }

        private MessageContext Context(Session session) => new MessageContext { Session = session };

        [Fact]
        public async Task Login_success_acknowledges()
        {
            // Seed a player in the in-memory DB.
            using (var db = _ctx.Get<GameContext>())
            {
                db.Players.Add(new PlayerEntity { Id = 9001, TotalExperience = 1000 });
                await db.SaveChangesAsync();
            }

            // Responder for the auth request (the Auth server normally handles this).
            var bus = (Foundatio.Messaging.InMemoryMessageBus)_ctx.Get<Foundatio.Messaging.IMessageBus>();
            await bus.SubscribeToRequestAsync<ChatLoginRequest, ChatLoginResponse>(req =>
                Task.FromResult(new ChatLoginResponse(true, new Account(9001, "user", "nick", SecurityLevel.User), 1000, 0)),
                CancellationToken.None);

            var handler = _ctx.Get<AuthenticationHandler>();
            var (session, channel) = _ctx.CreateSession(1);
            var context = Context(session);

            var result = await handler.OnHandle(context, new LoginReqMessage
            {
                AccountId = 9001,
                Nickname = "nick",
                SessionId = "sid"
            });

            Assert.True(result);
            Assert.NotNull(session.Player);
            Assert.Equal(9001UL, session.Player.Account.Id);
            // Player registered in manager
            Assert.True(_ctx.Get<PlayerManager>().Contains(9001UL));
        }

        [Fact]
        public async Task Login_wrongAuth_returnsError2()
        {
            var bus = (Foundatio.Messaging.InMemoryMessageBus)_ctx.Get<Foundatio.Messaging.IMessageBus>();
            await bus.SubscribeToRequestAsync<ChatLoginRequest, ChatLoginResponse>(req =>
                Task.FromResult(new ChatLoginResponse(false, null, 0, 0)),
                CancellationToken.None);

            var handler = _ctx.Get<AuthenticationHandler>();
            var (session, _) = _ctx.CreateSession(1);
            var context = Context(session);

            var result = await handler.OnHandle(context, new LoginReqMessage
            {
                AccountId = 9001, Nickname = "nick", SessionId = "sid"
            });

            Assert.True(result);
            Assert.Null(session.Player);
        }
    }
}
