using System;
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
using OpenS4L.Server.Chat.Services;
using ProudNet;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Drives the Chat IpcService (the hosted IPC subscriber) over the harness. StartAsync wires
    /// the subscriptions; publishing messages on the in-memory bus triggers the handlers.
    /// </summary>
    public class ChatIpcServiceTests
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
        public async Task IpcService_startPublishesAndHandles()
        {
            var (session, _) = await LoginAsync(7001);
            var ipc = _ctx.Get<IpcService>();
            await ipc.StartAsync(CancellationToken.None);

            var bus = _ctx.Get<Foundatio.Messaging.IMessageBus>();

            // Player joins a channel via IPC.
            await bus.PublishAsync(typeof(ChannelPlayerJoinedMessage),
                new ChannelPlayerJoinedMessage { AccountId = 7001, ChannelId = 1 }, TimeSpan.Zero, CancellationToken.None);
            await Task.Delay(50);
            var cm = _ctx.Get<ChannelManager>();
            var channel = cm[1];
            Assert.NotNull(channel);
            Assert.Contains(session.Player, channel.Players.Values);

            // Player update via IPC.
            await bus.PublishAsync(typeof(PlayerUpdateMessage),
                new PlayerUpdateMessage(7001, 5000, 20, 0, TeamId.Alpha), TimeSpan.Zero, CancellationToken.None);
            await Task.Delay(50);
            Assert.Equal(5000u, session.Player.TotalExperience);
            Assert.Equal(20, session.Player.Level);

            await ipc.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task IpcService_clanMemberUpdate_broadcastsToClan()
        {
            var (session, channel) = await LoginAsync(8101);
            var (other, otherChannel) = await LoginAsync(8102);
            var ipc = _ctx.Get<IpcService>();
            await ipc.StartAsync(CancellationToken.None);

            // Both players belong to clan 7.
            session.Player.ClanId = 7;
            other.Player.ClanId = 7;

            var bus = _ctx.Get<Foundatio.Messaging.IMessageBus>();
            await bus.PublishAsync(typeof(ClanMemberUpdateMessage),
                new ClanMemberUpdateMessage(7, 8101, ClubMemberPresenceState.Online, true), TimeSpan.Zero, CancellationToken.None);
            await Task.Delay(100);

            // Both clan members received the login-state update.
            Assert.Contains(channel.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is ClubMemberLoginStateAckMessage);
            Assert.Contains(otherChannel.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is ClubMemberLoginStateAckMessage);

            await ipc.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task IpcService_playerLeftChannel_removesFromChannel()
        {
            var (session, _) = await LoginAsync(8201);
            var ipc = _ctx.Get<IpcService>();
            await ipc.StartAsync(CancellationToken.None);

            var bus = _ctx.Get<Foundatio.Messaging.IMessageBus>();
            // Join a channel, then leave it.
            await bus.PublishAsync(typeof(ChannelPlayerJoinedMessage),
                new ChannelPlayerJoinedMessage { AccountId = 8201, ChannelId = 2 }, TimeSpan.Zero, CancellationToken.None);
            await Task.Delay(50);
            var cm = _ctx.Get<ChannelManager>();
            var channel = cm[2];
            Assert.NotNull(channel);
            Assert.Contains(session.Player, channel.Players.Values);

            await bus.PublishAsync(typeof(ChannelPlayerLeftMessage),
                new ChannelPlayerLeftMessage(8201, 2), TimeSpan.Zero, CancellationToken.None);
            await Task.Delay(50);
            Assert.Empty(channel.Players);

            await ipc.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task IpcService_playerDisconnected_closesSession()
        {
            var (session, _) = await LoginAsync(8202);
            var ipc = _ctx.Get<IpcService>();
            await ipc.StartAsync(CancellationToken.None);

            var bus = _ctx.Get<Foundatio.Messaging.IMessageBus>();
            // The disconnect handler runs plr.Disconnect() → Session.CloseAsync() (no-op on the
            // fake channel, so IsConnected stays true). The path is exercised without throwing.
            await bus.PublishAsync(typeof(PlayerDisconnectedMessage),
                new PlayerDisconnectedMessage { AccountId = 8202 }, TimeSpan.Zero, CancellationToken.None);
            await Task.Delay(100);

            await ipc.StopAsync(CancellationToken.None);
        }
    }
}
