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
    /// Drives the ClanHandler and the PlayerManager disconnect path over the harness.
    /// </summary>
    public class ChatClanAndDisconnectTests
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
        public async Task Clan_memberList_acknowledges()
        {
            var (session, channel) = await LoginAsync(5001);
            var bus = (Foundatio.Messaging.InMemoryMessageBus)_ctx.Get<Foundatio.Messaging.IMessageBus>();
            await bus.SubscribeToRequestAsync<ClanMemberListRequest, ClanMemberListResponse>(req =>
                Task.FromResult(new ClanMemberListResponse(new[]
                {
                    new ClanMemberInfo { AccountId = 1, Nickname = "m1", Role = ClubRole.Normal }
                })),
                CancellationToken.None);

            var handler = _ctx.Get<ClanHandler>();
            await handler.OnHandle(new MessageContext { Session = session }, new ClubMemberListReqMessage { ClanId = 5 });

            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<ClubMemberListAckMessage>().First();
            Assert.Single(ack.Members);
        }

        [Fact]
        public async Task Disconnect_savesAndRemovesPlayer()
        {
            var (session, _) = await LoginAsync(6001);
            var pm = _ctx.Get<PlayerManager>();
            Assert.True(pm.Contains(6001UL));

            // Simulate the transport removing the session. The session must be registered with the
            // session manager first (in production the transport does this on connect). The
            // PlayerManager.SessionDisconnected handler is async void + does a DB save, so poll.
            var sessionManager = (FakeSessionManager)_ctx.Get<ISessionManager>();
            sessionManager.AddSession(session);
            Assert.True(sessionManager.Sessions.ContainsKey(6001u));

            sessionManager.RemoveSession(6001);

            for (var i = 0; i < 50 && pm.Contains(6001UL); i++)
                await Task.Delay(20);

            Assert.False(pm.Contains(6001UL));
        }
    }
}
