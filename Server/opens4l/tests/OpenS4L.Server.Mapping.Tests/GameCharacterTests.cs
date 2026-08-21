using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Data.Game;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Exercises the Game Character layer over the harness: character creation, selection,
    /// and the CharacterFirstCreateReqMessage handler path.
    /// </summary>
    public class GameCharacterTests
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
            var (session, _) = _ctx.CreateSession(accountId);
            await handler.OnHandle(new MessageContext { Session = session }, new LoginRequestReqMessage
            {
                AccountId = accountId, SessionId = "sid-" + accountId, Version = new Version(1, 0, 0, 0)
            });
            return session.Player;
        }

        [Fact]
        public async Task CharacterManager_create_select()
        {
            var plr = await LoginAsync(9001);
            var cm = plr.CharacterManager;

            var (character, result) = cm.Create(0, CharacterGender.Male, 0, 0, 0, 0, 0, 0);
            Assert.Equal(CharacterCreateResult.Success, result);
            Assert.NotNull(character);
            Assert.Equal(1, cm.Count);

            // Selecting the created slot works.
            Assert.True(cm.Select(0));
            Assert.Equal(0, cm.CurrentSlot);
        }

        [Fact]
        public async Task CharacterManager_create_duplicateSlot_returnsSlotInUse()
        {
            var plr = await LoginAsync(9002);
            var cm = plr.CharacterManager;
            cm.Create(0, CharacterGender.Male, 0, 0, 0, 0, 0, 0);

            var (_, result) = cm.Create(0, CharacterGender.Male, 0, 0, 0, 0, 0, 0);
            Assert.Equal(CharacterCreateResult.SlotInUse, result);
        }

        [Fact]
        public async Task CharacterManager_remove_missingSlot_returnsFalse()
        {
            var plr = await LoginAsync(9003);
            var cm = plr.CharacterManager;
            Assert.False(cm.Remove((byte)3));
        }

        [Fact]
        public async Task Character_equipAndUnequipWeapon()
        {
            var plr = await LoginAsync(9201);
            var (character, result) = plr.CharacterManager.Create(0, CharacterGender.Male, 0, 0, 0, 0, 0, 0);
            Assert.Equal(CharacterCreateResult.Success, result);

            // Create a skill PlayerItem (Stash reflects the last-seeded shop item 3050001 = Skill)
            // and equip it.
            var item = GameFixtures.CreatePlayerItem(_ctx.GameData);
            Assert.Equal(ItemCategory.Skill, item.ItemNumber.Category);

            var equipErr = character.Equip(item, (byte)SkillSlot.Skill);
            Assert.Equal(CharacterInventoryError.OK, equipErr);
            Assert.Contains(item, character.Skills.GetItems());

            // Unequip it.
            var unequipErr = character.UnEquip(ItemCategory.Skill, (byte)SkillSlot.Skill);
            Assert.Equal(CharacterInventoryError.OK, unequipErr);
            Assert.DoesNotContain(item, character.Skills.GetItems());
        }

        [Fact]
        public async Task Character_getAttributeValue()
        {
            var plr = await LoginAsync(9203);
            var (character, _) = plr.CharacterManager.Create(0, CharacterGender.Male, 0, 0, 0, 0, 0, 0);
            plr.CharacterManager.Select(0);

            // With no equipped items, attribute value is 0.
            var hp = plr.GetAttributeValue(EffectType.HP);
            Assert.Equal(0f, hp);
        }

        [Fact]
        public async Task Character_equipInvalidCategory_returnsItemNotAllowed()
        {
            var plr = await LoginAsync(9204);
            var (character, _) = plr.CharacterManager.Create(0, CharacterGender.Male, 0, 0, 0, 0, 0, 0);

            // A non-equippable category returns ItemNotAllowed from UnEquip.
            var err = character.UnEquip((ItemCategory)99, 0);
            Assert.Equal(CharacterInventoryError.ItemNotAllowed, err);
        }

        [Fact]
        public async Task Character_firstCreate_handler()
        {
            var plr = await LoginAsync(9004);
            var handler = _ctx.Get<AuthenticationHandler>();
            var (session, _) = _ctx.CreateSession(9004);
            session.Player = plr;

            var result = await handler.OnHandle(new MessageContext { Session = session }, new CharacterFirstCreateReqMessage
            {
                Style = new CharacterStyle(CharacterGender.Male, 0, 0, 0, 0, 0),
                Items = new ItemNumber[] { new ItemNumber(2010001u) },
                Nickname = ""
            });
            Assert.True(result);
            Assert.NotEmpty(plr.CharacterManager);
        }
    }
}
