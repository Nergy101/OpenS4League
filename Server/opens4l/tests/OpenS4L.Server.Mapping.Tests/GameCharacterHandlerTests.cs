using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Drives the Game CharacterHandler (create/select/delete) over the harness.
    /// </summary>
    public class GameCharacterHandlerTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();

        private async Task<(Player plr, FakeSocketChannel channel)> LoginAsync(uint accountId)
        {
            var cache = (Foundatio.Caching.InMemoryCacheClient)_ctx.Get<Foundatio.Caching.ICacheClient>();
            await cache.SetAsync<string>(Constants.Cache.SessionKey(accountId), "sid-" + accountId);
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = (int)accountId, Username = "g" + accountId, Nickname = "nick" + accountId });
                await auth.SaveChangesAsync();
            }
            using (var db = _ctx.Get<GameContext>())
            {
                db.Players.Add(new PlayerEntity { Id = (int)accountId, TotalExperience = 1000 });
                await db.SaveChangesAsync();
            }

            var handler = _ctx.Get<AuthenticationHandler>();
            var (session, channel) = _ctx.CreateSession(accountId);
            await handler.OnHandle(new MessageContext { Session = session }, new LoginRequestReqMessage
            {
                AccountId = accountId, SessionId = "sid-" + accountId, Version = new Version(1, 0, 0, 0)
            });
            return (session.Player, channel);
        }

        [Fact]
        public async Task CreateCharacter_success()
        {
            var (plr, _) = await LoginAsync(1901);
            var handler = _ctx.Get<CharacterHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new CharacterCreateReqMessage
            {
                Slot = 0,
                Style = new CharacterStyle(CharacterGender.Male, 0, 0, 0, 0, 0)
            });
            Assert.Equal(1, plr.CharacterManager.Count);
        }

        [Fact]
        public async Task SelectCharacter_success()
        {
            var (plr, _) = await LoginAsync(1902);
            var (character, _) = plr.CharacterManager.Create(0, CharacterGender.Male, 0, 0, 0, 0, 0, 0);
            Assert.NotNull(character);

            var handler = _ctx.Get<CharacterHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new CharacterSelectReqMessage { Slot = 0 });
            Assert.Equal(0, plr.CharacterManager.CurrentSlot);
        }

        [Fact]
        public async Task SelectCharacter_invalidSlot_returnsError()
        {
            var (plr, channel) = await LoginAsync(1903);
            var handler = _ctx.Get<CharacterHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new CharacterSelectReqMessage { Slot = 5 });
            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<ServerResultAckMessage>().LastOrDefault();
            Assert.NotNull(ack);
        }

        [Fact]
        public async Task DeleteCharacter_success()
        {
            var (plr, _) = await LoginAsync(1904);
            var (character, _) = plr.CharacterManager.Create(0, CharacterGender.Male, 0, 0, 0, 0, 0, 0);
            Assert.NotNull(character);

            var handler = _ctx.Get<CharacterHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new CharacterDeleteReqMessage { Slot = 0 });
            Assert.Equal(0, plr.CharacterManager.Count);
        }
    }
}
