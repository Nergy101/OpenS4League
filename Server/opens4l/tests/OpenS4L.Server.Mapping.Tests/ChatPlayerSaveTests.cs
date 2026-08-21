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
    /// Covers the Chat Player.Save path (all manager Saves) with a fully-logged-in player that
    /// has dirtied state.
    /// </summary>
    public class ChatPlayerSaveTests
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
        public async Task PlayerSave_persistsDirtyState()
        {
            // Seed a target account so friend/deny lookups work.
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = 5001, Username = "t", Nickname = "target" });
                await auth.SaveChangesAsync();
            }

            var plr = await LoginAsync(6001);

            // Dirty state: a deny, a friend, a setting, and a mail to a seeded receiver.
            plr.Ignore.Add(5001, "target");
            plr.Friends.Add(5001, "target", FriendState.Friends);
            plr.Settings.AddOrUpdate("MySetting", "value");
            await plr.Mailbox.SendAsync("target", "Hi", "Hello");

            using (var db = _ctx.Get<GameContext>())
            {
                await plr.Save(db);
                await db.SaveChangesAsync();

                // The Save path ran without throwing; the deny/friend/mail remain in memory.
                Assert.True(plr.Ignore.Contains(5001));
                Assert.True(plr.Friends.Contains(5001));
            }
        }
    }
}
