using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Drives the Game Player domain methods (attributes, notices, console) over the harness.
    /// </summary>
    public class GamePlayerDomainTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();

        private async Task<Player> LoginAsync(uint accountId)
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
            return session.Player;
        }

        [Fact]
        public async Task GetAttributeValue_noCharacter_returnsZero()
        {
            var plr = await LoginAsync(2901);
            Assert.Equal(0f, plr.GetAttributeValue(EffectType.HP));
            Assert.Equal(0f, plr.GetAttributeRate(EffectType.HP));
        }

        [Fact]
        public async Task GetMaxHP_withCharacter_returnsDefault()
        {
            var plr = await LoginAsync(2902);
            var (character, result) = plr.CharacterManager.Create(0, CharacterGender.Male, 0, 0, 0, 0, 0, 0);
            Assert.Equal(CharacterCreateResult.Success, result);
            plr.CharacterManager.Select(0);

            // GAMETEMPO_FREE ActorDefaultHPMax=100, no HP items → 100.
            Assert.Equal(100f, plr.GetMaxHP());
        }

        [Fact]
        public async Task SendNotice_and_consoleMessage_acknowledge()
        {
            var plr = await LoginAsync(2903);
            plr.SendNotice("hello");
            plr.SendConsoleMessage("console");
        }

        [Fact]
        public async Task SendMoneyUpdate_acknowledges()
        {
            var plr = await LoginAsync(2904);
            plr.PEN = 12345;
            plr.AP = 678;
            plr.SendMoneyUpdate();
        }
    }
}
