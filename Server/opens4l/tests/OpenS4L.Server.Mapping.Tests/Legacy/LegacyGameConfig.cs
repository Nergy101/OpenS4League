using System.Linq;
using ExpressMapper;
using ExpressMapper.Extensions;
using OpenS4L.Network.Data.Club;
using OpenS4L.Network.Data.Game;
using OpenS4L.Network.Data.GameRule;
using OpenS4L.Server.Game;

namespace OpenS4L.Server.Mapping.Tests.Legacy
{
    /// <summary>
    /// The Game server's legacy ExpressMapper config, preserved verbatim from
    /// <c>OpenS4L.Server.Game.Program.ConfigureMapper</c>. Used by the differential tests as
    /// the "old behaviour" baseline now that production has migrated to Mapperly.
    /// </summary>
    internal static class LegacyGameConfig
    {
        public static void Register()
        {
            Mapper.Reset();

            Mapper.Register<Channel, ChannelInfoDto>()
                .Member(dest => dest.PlayerCount, src => src.Players.Count)
                .Function(dest => dest.IsClanChannel, src => src.Category == ChannelCategory.Club);

            Mapper.Register<PlayerItem, ItemDto>()
                .Member(dest => dest.ExpireTime,
                    src => src.ExpireDate == System.DateTimeOffset.MinValue
                        ? -1
                        : src.ExpireDate.ToUnixTimeSeconds())
                .Function(
                    dest => dest.Effects,
                    src => src.Effects.Select(x => new ItemEffectDto
                    {
                        Effect = x
                    }).OrderBy(x => x.Effect).ToArray()
                );

            Mapper.Register<Room, Room2Dto>()
                .Member(dest => dest.RoomId, src => src.Id)
                .Member(dest => dest.GameRule, src => src.Options.GameRule)
                .Member(dest => dest.Map, src => src.Options.Map)
                .Member(dest => dest.PlayerLimit, src => src.Options.PlayerLimit)
                .Member(dest => dest.Name, src => src.Options.Name)
                .Member(dest => dest.ItemLimit, src => src.Options.EquipLimit)
                .Member(dest => dest.PlayerCount, src => src.Players.Count(x => !x.Value.IsInGMMode))
                .Function(dest => dest.State, src => src.GameRule.StateMachine.GameState - 1)
                .Member(dest => dest.IsSpectatingEnabled, src => src.Options.IsSpectatingEnabled)
                .Function(dest => dest.Password, src => string.IsNullOrEmpty(src.Options.Password) ? "" : "***")
                .Function(dest => dest.Settings, src =>
                {
                    var settings = RoomSettings.None;
                    if (src.Options.IsFriendly)
                        settings |= RoomSettings.IsFriendly;

                    return settings;
                });

            Mapper.Register<Room, EnterRoomInfo2Dto>()
                .Member(dest => dest.RoomId, src => src.Id)
                .Member(dest => dest.GameRule, src => src.Options.GameRule)
                .Member(dest => dest.Map, src => src.Options.Map)
                .Member(dest => dest.PlayerLimit, src => src.Options.PlayerLimit)
                .Member(dest => dest.TimeLimit, src => src.Options.TimeLimit.TotalMilliseconds)
                .Member(dest => dest.TimeSync, src => src.GameRule.StateMachine.RoundTime.TotalMilliseconds)
                .Member(dest => dest.ScoreLimit, src => src.Options.ScoreLimit)
                .Member(dest => dest.RelayEndPoint, src => src.Options.RelayEndPoint)
                .Member(dest => dest.State, src => src.GameRule.StateMachine.GameState)
                .Member(dest => dest.TimeState, src => src.GameRule.StateMachine.TimeState);

            Mapper.Register<Player, RoomPlayerDto>()
                .Member(dest => dest.AccountId, src => src.Account.Id)
                .Member(dest => dest.Nickname, src => src.Account.Nickname)
                .Member(dest => dest.Slot, src => src.Slot)
                .Value(dest => dest.Unk2, (byte)144);

            Mapper.Register<RoomCreationOptions, ChangeRuleDto>()
                .Member(dest => dest.GameRule, src => src.GameRule)
                .Member(dest => dest.Map, src => src.Map)
                .Member(dest => dest.PlayerLimit, src => src.PlayerLimit)
                .Member(dest => dest.ScoreLimit, src => src.ScoreLimit)
                .Member(dest => dest.TimeLimit, src => src.TimeLimit)
                .Member(dest => dest.ItemLimit, src => src.EquipLimit)
                .Member(dest => dest.Password, src => src.Password)
                .Member(dest => dest.Name, src => src.Name)
                .Member(dest => dest.IsSpectatingEnabled, src => src.IsSpectatingEnabled)
                .Member(dest => dest.SpectatorLimit, src => src.SpectatorLimit);

            Mapper.Register<RoomCreationOptions, ChangeRule2Dto>()
                .Member(dest => dest.GameRule, src => src.GameRule)
                .Member(dest => dest.Map, src => src.Map)
                .Member(dest => dest.PlayerLimit, src => src.PlayerLimit)
                .Member(dest => dest.ScoreLimit, src => src.ScoreLimit)
                .Member(dest => dest.TimeLimit, src => src.TimeLimit)
                .Member(dest => dest.ItemLimit, src => src.EquipLimit)
                .Member(dest => dest.Password, src => src.Password)
                .Member(dest => dest.Name, src => src.Name)
                .Member(dest => dest.IsSpectatingEnabled, src => src.IsSpectatingEnabled)
                .Member(dest => dest.SpectatorLimit, src => src.SpectatorLimit)
                .Function(dest => dest.Settings, src =>
                {
                    var settings = RoomSettings.None;
                    if (src.IsFriendly)
                        settings |= RoomSettings.IsFriendly;

                    return settings;
                });

            Mapper.Register<Clan, ClubSearchResultDto>()
                .Function(dest => dest.OwnerName, src => src.Owner.Name)
                .Function(dest => dest.MemberCount, src => src.Count);

            Mapper.Register<ClanMember, JoinWaiterInfoDto>();

            Mapper.Compile(CompilationTypes.Source);
        }
    }
}
