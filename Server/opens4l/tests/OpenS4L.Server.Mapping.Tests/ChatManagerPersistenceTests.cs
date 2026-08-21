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
    /// Covers the Chat manager Initialize/Save persistence paths using a fully-logged-in player
    /// (so the managers are properly wired), then exercising Save against seeded in-memory entities.
    /// </summary>
    public class ChatManagerPersistenceTests
    {
        private readonly ChatTestContext _ctx = new ChatTestContext();

        private async Task<(Player player, Session session)> LoginAsync(ulong accountId)
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
            return (session.Player, session);
        }

        [Fact]
        public async Task DenyManager_addAndRemove()
        {
            var (player, _) = await LoginAsync(7001);
            var dm = player.Ignore;
            var deny = dm.Add(8001, "target");
            Assert.NotNull(deny);
            Assert.True(dm.Contains(8001));
            Assert.True(dm.Remove(8001));
            Assert.False(dm.Contains(8001));
        }

        [Fact]
        public async Task DenyManager_save_persists()
        {
            var (player, _) = await LoginAsync(7001);
            player.Ignore.Add(8001, "target");
            using (var db = _ctx.Get<GameContext>())
            {
                await player.Ignore.Save(db);
                await db.SaveChangesAsync();
            }
            using (var db = _ctx.Get<GameContext>())
            {
                Assert.Single(db.PlayerIgnores);
            }
        }

        [Fact]
        public async Task Mailbox_send_persistsMail()
        {
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 5001, Username = "recv", Nickname = "recv" });
                await auth.SaveChangesAsync();
            }
            var (player, _) = await LoginAsync(5002);

            var ok = await player.Mailbox.SendAsync("recv", "Hi", "Hello");
            Assert.True(ok);
        }

        [Fact]
        public async Task Mailbox_send_nonexistentReceiver_fails()
        {
            var (player, _) = await LoginAsync(5003);
            var ok = await player.Mailbox.SendAsync("ghost", "Hi", "Hello");
            Assert.False(ok);
        }

        [Fact]
        public async Task Mailbox_initialize_loadsMailFromSender()
        {
            // Seed the sender account + a mail to player 7005.
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 6001, Username = "s", Nickname = "senderNick" });
                await auth.SaveChangesAsync();
            }
            using (var db = _ctx.Get<GameContext>())
            {
                db.PlayerMails.Add(new PlayerMailEntity
                {
                    Id = 1, PlayerId = 7005, SenderPlayerId = 6001,
                    Title = "Welcome", Message = "Hello there", IsMailNew = true, IsMailDeleted = false
                });
                await db.SaveChangesAsync();
            }

            var (player, _) = await LoginAsync(7005);
            // Login initialized the mailbox from the seeded mail.
            Assert.Equal(1, player.Mailbox.Count);
            var mail = player.Mailbox[1];
            Assert.NotNull(mail);
            Assert.Equal("senderNick", mail.Sender);
            Assert.Equal("Hello there", mail.Message);
        }

        [Fact]
        public async Task Mailbox_removeTracksForDelete()
        {
            var (player, _) = await LoginAsync(7006);
            // No mails present → remove returns false.
            Assert.False(player.Mailbox.Remove(new long[] { 1, 2 }));
        }

        [Fact]
        public async Task PlayerSettingManager_updateExisting_savesDirty()
        {
            // Seed a setting entity for player 7007.
            using (var db = _ctx.Get<GameContext>())
            {
                db.PlayerSettings.Add(new PlayerSettingEntity { Id = 1, PlayerId = 7007, Setting = "MySetting", Value = "old" });
                await db.SaveChangesAsync();
            }

            var (player, _) = await LoginAsync(7007);
            // The login loaded the setting; update it to mark it dirty, then save.
            player.Settings.AddOrUpdate("MySetting", "new");

            using (var db = _ctx.Get<GameContext>())
            {
                await player.Settings.Save(db);
                await db.SaveChangesAsync();
            }

            Assert.Equal("new", player.Settings.Get("MySetting"));
        }

        [Fact]
        public async Task DenyManager_initialize_loadsDenies()
        {
            // Seed a deny entity + the target account.
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 8001, Username = "t", Nickname = "target" });
                await auth.SaveChangesAsync();
            }
            using (var db = _ctx.Get<GameContext>())
            {
                db.PlayerIgnores.Add(new PlayerDenyEntity { Id = 1, PlayerId = 7008, DenyPlayerId = 8001 });
                await db.SaveChangesAsync();
            }

            var (player, _) = await LoginAsync(7008);
            // Login initialized the deny manager from the seeded entity.
            Assert.True(player.Ignore.Contains(8001));
        }

        [Fact]
        public async Task PlayerSettingManager_addAndUpdate()
        {
            var (player, _) = await LoginAsync(5004);
            var sm = player.Settings;
            sm.AddOrUpdate("MySetting", "value");
            Assert.Equal("value", sm.Get("MySetting"));
            sm.AddOrUpdate("MySetting", "new");
            Assert.Equal("new", sm.Get("MySetting"));
        }
    }
}
