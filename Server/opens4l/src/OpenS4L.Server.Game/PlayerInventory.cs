using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenS4L.Blub.Collections.Concurrent;
using Logging;
using Microsoft.EntityFrameworkCore;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Game;
using OpenS4L.Network.Data.Game;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game.Data;
using OpenS4L.Server.Game.Mappers;
using OpenS4L.Server.Game.Services;
using Newtonsoft.Json;

namespace OpenS4L.Server.Game
{
    public class PlayerInventory : IReadOnlyCollection<PlayerItem>
    {
        private readonly GameDataService _gameDataService;
        private readonly IdGeneratorService _idGeneratorService;
        private readonly GameMapper _mapper;
        private readonly ConcurrentDictionary<ulong, PlayerItem> _items;
        private readonly ConcurrentStack<PlayerItem> _itemsToRemove;
        private ILogger _logger;

        public Player Player { get; private set; }
        public int Count => _items.Count;

        /// <summary>
        /// True when any item is dirty or queued for removal (i.e. this inventory has unsaved
        /// changes that need flushing on the next save cycle).
        /// </summary>
        public bool HasPendingChanges => !_itemsToRemove.IsEmpty || _items.Values.Any(x => x.IsDirty);

        /// <summary>
        /// Returns the item with the given id or null if not found
        /// </summary>
        public PlayerItem this[ulong id] => GetItem(id);

        public PlayerInventory(ILogger<PlayerInventory> logger, GameDataService gameDataService,
            IdGeneratorService idGeneratorService, GameMapper mapper)
        {
            _logger = logger;
            _gameDataService = gameDataService;
            _idGeneratorService = idGeneratorService;
            _mapper = mapper;
            _items = new ConcurrentDictionary<ulong, PlayerItem>();
            _itemsToRemove = new ConcurrentStack<PlayerItem>();
        }

        internal void Initialize(Player plr, PlayerEntity entity)
        {
            Player = plr;
            _logger = plr.AddContextToLogger(_logger);

            foreach (var item in entity.Items.Select(x => new PlayerItem(_logger, _gameDataService, this, x)))
                _items.TryAdd(item.Id, item);
        }

        /// <summary>
        /// Returns the item with the given id or null if not found
        /// </summary>
        public PlayerItem GetItem(ulong id)
        {
            _items.TryGetValue(id, out var item);
            return item;
        }

        /// <summary>
        /// Creates a new item
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public PlayerItem Create(ItemNumber itemNumber, ItemPriceType priceType, ItemPeriodType periodType, ushort period,
            byte color, uint[] effects, uint count, bool sendUpdate = true)
        {
            // TODO Remove exceptions and instead return a error code

            var shopItemInfo = _gameDataService.GetShopItemInfo(itemNumber, priceType);
            if (shopItemInfo == null)
                throw new ArgumentException("Item not found");

            var price = shopItemInfo.PriceGroup.GetPrice(periodType, period);
            if (price == null)
                throw new ArgumentException("Price not found");

            return Create(shopItemInfo, price, color, effects, sendUpdate);
        }

        /// <summary>
        /// Creates a new item
        /// </summary>
        public PlayerItem Create(ShopItemInfo shopItemInfo, ShopPrice price, byte color, uint[] effects, bool sendUpdate = true)
        {
            var itemId = _idGeneratorService.GetNextId(IdKind.Item);
            var item = new PlayerItem(_gameDataService, this,
                itemId, shopItemInfo, price, color, effects, DateTimeOffset.Now);
            _items.TryAdd(item.Id, item);

            if (sendUpdate)
                Player.Session.Send(new ItemUpdateInventoryAckMessage(InventoryAction.Add, _mapper.ToItemDto(item)));

            return item;
        }

        /// <summary>
        /// Removes the item from the inventory
        /// </summary>
        public bool Remove(PlayerItem item)
        {
            return Remove(item.Id);
        }

        /// <summary>
        /// Removes the item from the inventory
        /// </summary>
        public bool Remove(ulong id)
        {
            var item = GetItem(id);
            if (item == null)
                return false;

            _items.Remove(item.Id);
            if (item.Exists)
                _itemsToRemove.Push(item);

            Player.Session.Send(new ItemInventroyDeleteAckMessage(item.Id));
            return true;
        }

        /// <summary>
        /// Captures the inventory's unsaved state into a snapshot (pure read — does not mutate
        /// the live inventory). New and dirty-existing items are included so the writer can
        /// reproduce the old <c>Save</c> exactly; unchanged existing items are skipped.
        /// </summary>
        public void BuildSnapshot(PlayerSaveSnapshot s)
        {
            foreach (var itemToRemove in _itemsToRemove)
                s.ItemIdsToRemove.Add((long)itemToRemove.Id);

            foreach (var item in _items.Values)
            {
                if (!item.Exists)
                {
                    s.Items.Add(new SnapshotItem
                    {
                        Id = (long)item.Id,
                        ShopItemInfoId = item.GetShopItemInfo().Id,
                        ShopPriceId = item.GetShopItemInfo().PriceGroup.GetPrice(item.PeriodType, item.Period).Id,
                        Effects = JsonConvert.SerializeObject(item.Effects.ToArray()),
                        Color = item.Color,
                        PurchaseDate = item.PurchaseDate.ToUnixTimeSeconds(),
                        Durability = item.Durability,
                        MP = (int)item.EnchantMP,
                        MPLevel = (int)item.EnchantLevel,
                        Exists = false
                    });
                }
                else if (item.IsDirty)
                {
                    s.Items.Add(new SnapshotItem
                    {
                        Id = (long)item.Id,
                        ShopItemInfoId = item.GetShopItemInfo().Id,
                        ShopPriceId = item.GetShopPrice().Id,
                        Effects = JsonConvert.SerializeObject(item.Effects.ToArray()),
                        Color = item.Color,
                        PurchaseDate = item.PurchaseDate.ToUnixTimeSeconds(),
                        Durability = item.Durability,
                        MP = (int)item.EnchantMP,
                        MPLevel = (int)item.EnchantLevel,
                        Exists = true
                    });
                }
            }
        }

        /// <summary>
        /// Marks the captured snapshot as persisted: drains the remove-stack, flags new items as
        /// existing, and clears item dirty flags. Called only after the snapshot was durably
        /// enqueued (or written directly), so a failed publish leaves state dirty for retry.
        /// </summary>
        public void ClearPendingChanges()
        {
            while (_itemsToRemove.TryPop(out _)) { }

            foreach (var item in _items.Values)
            {
                if (!item.Exists)
                    item.SetExistsState(true);
                else
                    item.SetDirtyState(false);
            }
        }

        public bool Contains(ulong id)
        {
            return _items.ContainsKey(id);
        }

        public IEnumerator<PlayerItem> GetEnumerator()
        {
            return _items.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
