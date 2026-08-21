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
using OpenS4L.Network.Message.Chat;
using OpenS4L.Server.Chat;
using OpenS4L.Server.Chat.Handlers;
using ProudNet;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Drives the PrivateMessageHandler (notes) end-to-end over the harness.
    /// </summary>
    public class ChatPrivateMessageHandlerTests
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
        public async Task Note_list_empty_acknowledges()
        {
            var (session, channel) = await LoginAsync(3001);
            var handler = _ctx.Get<PrivateMessageHandler>();
            await handler.OnHandle(new MessageContext { Session = session }, new NoteListReqMessage { Page = 1, MessageType = 0 });
            Assert.Single(channel.Outbound.Where(o => o.GetType().GetProperty("Message")?.GetValue(o) is NoteListAckMessage));
        }

        [Fact]
        public async Task Note_read_missing_acknowledgesError()
        {
            var (session, channel) = await LoginAsync(3002);
            var handler = _ctx.Get<PrivateMessageHandler>();
            await handler.OnHandle(new MessageContext { Session = session }, new NoteReadReqMessage { Id = 999 });
            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<NoteReadAckMessage>().First();
            Assert.Equal(1, ack.Unk);
        }

        [Fact]
        public async Task Note_send_success_acknowledges()
        {
            // Receiver account in the DB.
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 4001, Username = "recv", Nickname = "recv" });
                await auth.SaveChangesAsync();
            }

            var (session, channel) = await LoginAsync(4002);
            var handler = _ctx.Get<PrivateMessageHandler>();
            await handler.OnHandle(new MessageContext { Session = session }, new NoteSendReqMessage
            {
                Receiver = "recv", Title = "Hi", Message = "Hello there"
            });

            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<NoteSendAckMessage>().First();
            Assert.Equal(0, ack.Result);
        }

        [Fact]
        public async Task Note_send_nonexistentReceiver_returnsError()
        {
            var (session, channel) = await LoginAsync(4003);
            var handler = _ctx.Get<PrivateMessageHandler>();
            await handler.OnHandle(new MessageContext { Session = session }, new NoteSendReqMessage
            {
                Receiver = "ghost", Title = "Hi", Message = "Hello"
            });

            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<NoteSendAckMessage>().First();
            Assert.Equal(1, ack.Result);
        }

        [Fact]
        public async Task Note_delete_acknowledges()
        {
            var (session, channel) = await LoginAsync(4004);
            var handler = _ctx.Get<PrivateMessageHandler>();
            await handler.OnHandle(new MessageContext { Session = session }, new NoteDeleteReqMessage { Notes = new ulong[] { 1, 2 } });
            Assert.Single(channel.Outbound.Where(o => o.GetType().GetProperty("Message")?.GetValue(o) is NoteDeleteAckMessage));
        }

        [Fact]
        public async Task Note_readAndList_existingMail()
        {
            // Two players; sender sends a mail to the logged-in player.
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 5001, Username = "recv", Nickname = "nick5001" });
                auth.Accounts.Add(new AccountEntity { Id = 5002, Username = "recv2", Nickname = "nick5002" });
                await auth.SaveChangesAsync();
            }
            var (sender, _) = await LoginAsync(5001);
            var (receiver, receiverCh) = await LoginAsync(5002);

            // Send a mail to the receiver (who is online → added to their mailbox).
            await sender.Player.Mailbox.SendAsync("nick5002", "Hello", "World");
            await Task.Delay(50);

            var handler = _ctx.Get<PrivateMessageHandler>();
            // List the mail.
            await handler.OnHandle(new MessageContext { Session = receiver }, new NoteListReqMessage { Page = 1, MessageType = 0 });
            var listAck = receiverCh.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<NoteListAckMessage>().FirstOrDefault();
            Assert.NotNull(listAck);
            Assert.NotEmpty(listAck.Notes);

            // Read the mail → marks it not-new and sends the content.
            var mailId = listAck.Notes[0].Id;
            await handler.OnHandle(new MessageContext { Session = receiver }, new NoteReadReqMessage { Id = mailId });
            var readAck = receiverCh.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<NoteReadAckMessage>().LastOrDefault();
            Assert.NotNull(readAck);
            Assert.Equal("World", readAck.Note.Message);
        }

        [Fact]
        public async Task Note_delete_existingMail()
        {
            // Sender sends a mail to the receiver (online).
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 5101, Username = "a", Nickname = "nick5101" });
                auth.Accounts.Add(new AccountEntity { Id = 5102, Username = "b", Nickname = "nick5102" });
                await auth.SaveChangesAsync();
            }
            var (sender, _) = await LoginAsync(5101);
            var (receiver, receiverCh) = await LoginAsync(5102);
            await sender.Player.Mailbox.SendAsync("nick5102", "Hi", "Msg");
            await Task.Delay(50);

            var handler = _ctx.Get<PrivateMessageHandler>();
            await handler.OnHandle(new MessageContext { Session = receiver }, new NoteListReqMessage { Page = 1, MessageType = 0 });
            var listAck = receiverCh.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<NoteListAckMessage>().LastOrDefault();
            Assert.NotNull(listAck);
            Assert.NotEmpty(listAck.Notes);

            // Delete the mail → removes it from the mailbox.
            var mailId = listAck.Notes[0].Id;
            await handler.OnHandle(new MessageContext { Session = receiver }, new NoteDeleteReqMessage { Notes = new ulong[] { mailId } });
            Assert.True(receiver.Player.Mailbox.Count == 0);
        }
    }
}
