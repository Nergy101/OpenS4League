using System.Linq;
using ExpressMapper;
using ExpressMapper.Extensions;
using OpenS4L.Plugins.WebApi.Models;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Data;
using OpenS4L.Server.Game.Services;
using ExpressMapper.Extensions;

namespace OpenS4L.Server.Mapping.Tests.Legacy
{
    /// <summary>
    /// The WebApi plugin's legacy ExpressMapper config, preserved verbatim from
    /// <c>WebApiService.RegisterMappers</c>. Used by the differential tests as the "old
    /// behaviour" baseline now that production has migrated to Mapperly. The GameDataService is
    /// dereferenced only inside deferred mapping lambdas (e.g. Items[...] lookups).
    /// </summary>
    internal static class LegacyWebApiConfig
    {
        public static void Register(GameDataService gameDataService)
        {
            Mapper.Reset();

            Mapper.Register<MapInfo, MapDto>();

            Mapper.Register<ItemInfo, ItemDto>()
                .Member(dest => dest.Id, src => src.ItemNumber.Id);

            Mapper.Register<DefaultItem, DefaultItemDto>()
                .Function(dest => dest.Item, src =>
                {
                    var itemInfo = gameDataService.Items[src.ItemNumber];
                    return itemInfo.Map<ItemInfo, ItemDto>();
                });

            Mapper.Register<Channel, ChannelDto>()
                .Member(dest => dest.PlayersOnline, src => src.Players.Count);

            Mapper.Register<Player, PlayerDto>()
                .Member(dest => dest.Id, src => src.Account.Id)
                .Member(dest => dest.Username, src => src.Account.Username)
                .Member(dest => dest.Nickname, src => src.Account.Nickname)
                .Function(dest => dest.ActiveCharacter, src => src.CharacterManager.CurrentCharacter?.Slot ?? 0)
                .Function(dest => dest.Characters,
                    src => src.CharacterManager.Select(x => x.Map<Character, CharacterDto>()).ToArray())
                .Function(dest => dest.Inventory,
                    src => src.Inventory.Select(x => x.Map<PlayerItem, PlayerItemDto>()).ToArray())
                .Function(dest => dest.ChannelId, src => src.Channel?.Id)
                .Function(dest => dest.RoomId, src => src.Room?.Id);

            Mapper.Register<Character, CharacterDto>()
                .Function(dest => dest.Weapons, src => src.Weapons.GetItems().Select(x => x?.Id ?? 0).ToArray())
                .Function(dest => dest.Skills, src => src.Skills.GetItems().Select(x => x?.Id ?? 0).ToArray())
                .Function(dest => dest.Costumes, src => src.Costumes.GetItems().Select(x => x?.Id ?? 0).ToArray());

            Mapper.Register<PlayerItem, PlayerItemDto>()
                .Function(dest => dest.Item, src =>
                {
                    var itemInfo = gameDataService.Items[src.ItemNumber];
                    return itemInfo.Map<ItemInfo, ItemDto>();
                })
                .Function(dest => dest.PurchaseTimestamp, src => src.PurchaseDate.ToUnixTimeSeconds());

            Mapper.Register<Room, RoomDto>()
                .Member(dest => dest.Name, src => src.Options.Name)
                .Member(dest => dest.CreationTimestamp, src => (int)new System.DateTimeOffset(src.TimeCreated).ToUnixTimeSeconds())
                .Member(dest => dest.MasterId, src => src.Master.Account.Id)
                .Member(dest => dest.HostId, src => src.Host.Account.Id)
                .Member(dest => dest.GameRule, src => src.GameRule.GameRule)
                .Function(dest => dest.State, src => src.GameRule.StateMachine.GameState)
                .Function(dest => dest.TimeState, src => src.GameRule.StateMachine.TimeState)
                .Member(dest => dest.PlayerLimit, src => src.Options.PlayerLimit)
                .Member(dest => dest.SpectatorLimit, src => src.Options.SpectatorLimit)
                .Member(dest => dest.Password, src => src.Options.Password)
                .Member(dest => dest.TimeLimit, src => src.Options.TimeLimit.TotalMinutes)
                .Member(dest => dest.ScoreLimit, src => src.Options.ScoreLimit)
                .Member(dest => dest.IsFriendly, src => src.Options.IsFriendly)
                .Member(dest => dest.EquipLimit, src => src.Options.EquipLimit)
                .Function(dest => dest.Players,
                    src => src.Players.Values.Select(x => x.Map<Player, RoomPlayerDto>()).ToArray());

            Mapper.Register<Player, RoomPlayerDto>()
                .Member(dest => dest.TeamId, src => src.Team.Id)
                .Member(dest => dest.Id, src => src.Account.Id)
                .Member(dest => dest.Username, src => src.Account.Username)
                .Member(dest => dest.Nickname, src => src.Account.Nickname)
                .Function(dest => dest.ActiveCharacter, src => src.CharacterManager.CurrentCharacter?.Slot ?? 0)
                .Function(dest => dest.Characters,
                    src => src.CharacterManager.Select(x => x.Map<Character, CharacterDto>()).ToArray())
                .Function(dest => dest.Inventory,
                    src => src.Inventory.Select(x => x.Map<PlayerItem, PlayerItemDto>()).ToArray());

            Mapper.Compile(CompilationTypes.Source);
        }
    }
}
