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
    /// Drives the Game InventoryHandler (use/repair/refund/discard item) over the harness.
    /// </summary>
    public class GameInventoryHandlerTests
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
        public async Task UseItem_unequipZero_acknowledges()
        {
            var (plr, channel) = await LoginAsync(1801);
            var handler = _ctx.Get<InventoryHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ItemUseItemReqMessage
            {
                CharacterSlot = 0, ItemId = 0, EquipSlot = 0, Action = UseItemAction.UnEquip
            });
            Assert.Contains(channel.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is ItemUseItemAckMessage);
        }

        [Fact]
        public async Task RepairItem_missingItem_returnsError()
        {
            var (plr, channel) = await LoginAsync(1802);
            var handler = _ctx.Get<InventoryHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ItemRepairItemReqMessage
            {
                Items = new ulong[] { 999999 }
            });
            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<ItemRepairItemAckMessage>().LastOrDefault();
            Assert.NotNull(ack);
            Assert.Equal(ItemRepairResult.Error0, ack.Result);
        }

        [Fact]
        public async Task RefundItem_missingItem_returnsFailed()
        {
            var (plr, channel) = await LoginAsync(1803);
            var handler = _ctx.Get<InventoryHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ItemRefundItemReqMessage { ItemId = 999999 });
            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<ItemRefundItemAckMessage>().LastOrDefault();
            Assert.NotNull(ack);
            Assert.Equal(ItemRefundResult.Failed, ack.Result);
        }

        [Fact]
        public async Task DiscardItem_missingItem_returnsError()
        {
            var (plr, channel) = await LoginAsync(1804);
            var handler = _ctx.Get<InventoryHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ItemDiscardItemReqMessage { ItemId = 999999 });
            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<ItemDiscardItemAckMessage>().LastOrDefault();
            Assert.NotNull(ack);
        }

        [Fact]
        public async Task UseItem_equipWeapon_equips()
        {
            var (plr, _) = await LoginAsync(1805);
            // Create a character and a weapon item, then equip it via the handler.
            var (character, result) = plr.CharacterManager.Create(0, CharacterGender.Male, 0, 0, 0, 0, 0, 0);
            Assert.Equal(CharacterCreateResult.Success, result);
            plr.CharacterManager.Select(0);

            var item = plr.Inventory.Create((ItemNumber)2010001u, ItemPriceType.PEN, ItemPeriodType.None, 0, 0, Array.Empty<uint>(), 1, false); // weapon item
            Assert.NotNull(item);

            var handler = _ctx.Get<InventoryHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new ItemUseItemReqMessage
            {
                CharacterSlot = 0, ItemId = item.Id, EquipSlot = (byte)WeaponSlot.Weapon1, Action = UseItemAction.Equip
            });
            Assert.Contains(item, character.Weapons.GetItems());
        }
    }
}
