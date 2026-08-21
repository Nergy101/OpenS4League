using System;
using System.Linq;
using OpenS4L;
using OpenS4L.Plugins.WebApi.Models;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Data;
using OpenS4L.Server.Game.Services;
using Riok.Mapperly.Abstractions;

namespace OpenS4L.Plugins.WebApi.Mappers
{
    /// <summary>
    /// Mapperly replacement for the ExpressMapper registrations previously in
    /// <c>WebApiService.RegisterMappers</c>. Produces byte-for-byte the same DTOs as the
    /// legacy config (verified by the differential tests in OpenS4L.Server.Mapping.Tests).
    /// </summary>
    [Mapper]
    public partial class WebApiMapper
    {
        private readonly GameDataService _gameDataService;

        public WebApiMapper(GameDataService gameDataService)
        {
            _gameDataService = gameDataService;
        }

        // MapInfo -> MapDto
        public MapDto ToMapDto(MapInfo map)
        {
            // NOTE: legacy `Register<MapInfo, MapDto>()` had no explicit members, so ExpressMapper
            // auto-mapped same-named properties. MapInfo has no GameRules property, so GameRules was
            // always left null. We reproduce that EXACT behaviour (GameRules = null) to keep the
            // migration wire-identical; the never-populated GameRules is a pre-existing latent bug.
            return new MapDto
            {
                Id = map.Id,
                Name = map.Name
            };
        }

        // ItemInfo -> ItemDto
        public ItemDto ToItemDto(ItemInfo item)
        {
            return new ItemDto
            {
                Id = item.ItemNumber.Id,
                Name = item.Name,
                Gender = item.Gender
            };
        }

        // DefaultItem -> DefaultItemDto
        public DefaultItemDto ToDefaultItemDto(DefaultItem item)
        {
            return new DefaultItemDto
            {
                Item = ToItemDto(_gameDataService.Items[item.ItemNumber]),
                Variation = item.Variation
            };
        }

        // Character -> CharacterDto
        public CharacterDto ToCharacterDto(Character character)
        {
            return new CharacterDto
            {
                Id = character.Id,
                Slot = character.Slot,
                Gender = character.Gender,
                Hair = ToDefaultItemDto(character.Hair),
                Face = ToDefaultItemDto(character.Face),
                Shirt = ToDefaultItemDto(character.Shirt),
                Pants = ToDefaultItemDto(character.Pants),
                Gloves = ToDefaultItemDto(character.Gloves),
                Shoes = ToDefaultItemDto(character.Shoes),
                Weapons = character.Weapons.GetItems().Select(x => x?.Id ?? 0).ToArray(),
                Skills = character.Skills.GetItems().Select(x => x?.Id ?? 0).ToArray(),
                Costumes = character.Costumes.GetItems().Select(x => x?.Id ?? 0).ToArray()
            };
        }

        // PlayerItem -> PlayerItemDto
        public PlayerItemDto ToPlayerItemDto(PlayerItem item)
        {
            return new PlayerItemDto
            {
                Id = item.Id,
                Item = ToItemDto(_gameDataService.Items[item.ItemNumber]),
                PriceType = item.PriceType,
                PeriodType = item.PeriodType,
                Period = item.Period,
                Color = item.Color,
                Effect = item.Effects.FirstOrDefault(),
                PurchaseTimestamp = item.PurchaseDate.ToUnixTimeSeconds(),
                Durability = item.Durability,
                Count = 1
            };
        }

        // Player -> PlayerDto (WebApi)
        public PlayerDto ToPlayerDto(Player player)
        {
            return new PlayerDto
            {
                Id = player.Account.Id,
                Username = player.Account.Username,
                Nickname = player.Account.Nickname,
                Level = player.Level,
                TotalExperience = (int)player.TotalExperience,
                PEN = (int)player.PEN,
                AP = (int)player.AP,
                ActiveCharacter = (byte)(player.CharacterManager.CurrentCharacter?.Slot ?? 0),
                Characters = player.CharacterManager
                    .Select(x => ToCharacterDto(x))
                    .ToArray(),
                Inventory = player.Inventory
                    .Select(x => ToPlayerItemDto(x))
                    .ToArray(),
                ChannelId = player.Channel?.Id,
                RoomId = player.Room?.Id
            };
        }

        // Player -> RoomPlayerDto (WebApi)
        public RoomPlayerDto ToRoomPlayerDto(Player player)
        {
            return new RoomPlayerDto
            {
                Id = player.Account.Id,
                Username = player.Account.Username,
                Nickname = player.Account.Nickname,
                Level = player.Level,
                TotalExperience = (int)player.TotalExperience,
                PEN = (int)player.PEN,
                AP = (int)player.AP,
                ActiveCharacter = (byte)(player.CharacterManager.CurrentCharacter?.Slot ?? 0),
                Characters = player.CharacterManager
                    .Select(x => ToCharacterDto(x))
                    .ToArray(),
                Inventory = player.Inventory
                    .Select(x => ToPlayerItemDto(x))
                    .ToArray(),
                ChannelId = player.Channel?.Id,
                RoomId = player.Room?.Id,
                TeamId = player.Team?.Id ?? default
            };
        }

        // Channel -> ChannelDto (WebApi)
        public ChannelDto ToChannelDto(Channel channel)
        {
            return new ChannelDto
            {
                Id = channel.Id,
                Category = channel.Category,
                Name = channel.Name,
                PlayerLimit = channel.PlayerLimit,
                PlayersOnline = channel.Players.Count
            };
        }

        // Room -> RoomDto (WebApi)
        public RoomDto ToRoomDto(Room room)
        {
            return new RoomDto
            {
                Id = (int)room.Id,
                Name = room.Options.Name,
                CreationTimestamp = (int)new DateTimeOffset(room.TimeCreated).ToUnixTimeSeconds(),
                MasterId = room.Master?.Account.Id ?? 0,
                HostId = room.Host?.Account.Id ?? 0,
                Map = ToMapDto(room.Map),
                GameRule = room.GameRule.GameRule,
                State = room.GameRule.StateMachine.GameState,
                TimeState = room.GameRule.StateMachine.TimeState,
                PlayerLimit = room.Options.PlayerLimit,
                SpectatorLimit = room.Options.SpectatorLimit,
                Password = room.Options.Password,
                TimeLimit = (int)room.Options.TimeLimit.TotalMinutes,
                ScoreLimit = room.Options.ScoreLimit,
                IsFriendly = room.Options.IsFriendly,
                EquipLimit = room.Options.EquipLimit,
                Players = room.Players.Values.Select(x => ToRoomPlayerDto(x)).ToArray()
            };
        }
    }
}
