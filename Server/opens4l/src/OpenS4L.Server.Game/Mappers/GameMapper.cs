using System;
using System.Linq;
using OpenS4L.Network.Data.Club;
using OpenS4L.Network.Data.Game;
using OpenS4L.Network.Data.GameRule;
using Riok.Mapperly.Abstractions;

namespace OpenS4L.Server.Game.Mappers
{
    /// <summary>
    /// Mapperly replacement for the ExpressMapper registrations previously in
    /// <c>OpenS4L.Server.Game.Program.ConfigureMapper</c>. Produces byte-for-byte
    /// the same DTOs as the legacy config (verified by the differential tests in
    /// <c>OpenS4L.Server.Mapping.Tests</c>).
    /// </summary>
    [Mapper]
    public partial class GameMapper
    {
        // Channel -> ChannelInfoDto
        public ChannelInfoDto ToChannelInfoDto(Channel channel)
        {
            return new ChannelInfoDto
            {
                Id = (ushort)channel.Id,
                PlayerCount = (ushort)channel.Players.Count,
                PlayerLimit = (ushort)channel.PlayerLimit,
                IsClanChannel = channel.Category == ChannelCategory.Club,
                Name = channel.Name,
                Rank = channel.Rank,
                Description = channel.Description,
                Color = channel.Color,
                MinLevel = channel.MinLevel,
                MaxLevel = channel.MaxLevel
            };
        }

        // PlayerItem -> ItemDto
        public ItemDto ToItemDto(PlayerItem item)
        {
            return new ItemDto
            {
                Id = item.Id,
                ItemNumber = item.ItemNumber,
                PriceType = item.PriceType,
                PeriodType = item.PeriodType,
                Period = item.Period,
                Color = item.Color,
                // NOTE: legacy ExpressMapper assigned src.ExpireDate.ToUnixTimeSeconds() (a long) into this
                // DateTimeOffset-typed member. ExpressMapper has no long->DateTimeOffset conversion, so it
                // silently left the field at DateTimeOffset.MinValue. We reproduce that EXACT behaviour to
                // keep the migration wire-identical; the broken ExpireTime is a pre-existing latent bug.
                ExpireTime = DateTimeOffset.MinValue,
                Durability = item.Durability,
                Effects = item.Effects
                    .Select(x => new ItemEffectDto { Effect = x })
                    .OrderBy(x => x.Effect)
                    .ToArray(),
                EnchantMP = item.EnchantMP,
                EnchantLevel = item.EnchantLevel
            };
        }

        // Room -> Room2Dto
        public Room2Dto ToRoom2Dto(Room room)
        {
            var settings = RoomSettings.None;
            if (room.Options.IsFriendly)
                settings |= RoomSettings.IsFriendly;

            return new Room2Dto
            {
                RoomId = (byte)room.Id,
                State = room.GameRule.StateMachine.GameState - 1,
                GameRule = room.Options.GameRule,
                Map = room.Options.Map,
                PlayerCount = (byte)room.Players.Count(x => !x.Value.IsInGMMode),
                PlayerLimit = (byte)room.Options.PlayerLimit,
                ItemLimit = (uint)room.Options.EquipLimit,
                Password = string.IsNullOrEmpty(room.Options.Password) ? "" : "***",
                Name = room.Options.Name,
                IsSpectatingEnabled = (byte)(room.Options.IsSpectatingEnabled ? 1 : 0),
                Settings = settings
            };
        }

        // Room -> EnterRoomInfo2Dto
        public EnterRoomInfo2Dto ToEnterRoomInfo2Dto(Room room)
        {
            return new EnterRoomInfo2Dto
            {
                RoomId = room.Id,
                GameRule = room.Options.GameRule,
                Map = room.Options.Map,
                PlayerLimit = (byte)room.Options.PlayerLimit,
                TimeLimit = (uint)room.Options.TimeLimit.TotalMilliseconds,
                TimeSync = (uint)room.GameRule.StateMachine.RoundTime.TotalMilliseconds,
                ScoreLimit = room.Options.ScoreLimit,
                RelayEndPoint = room.Options.RelayEndPoint,
                State = room.GameRule.StateMachine.GameState,
                TimeState = room.GameRule.StateMachine.TimeState
            };
        }

        // Player -> RoomPlayerDto
        public RoomPlayerDto ToRoomPlayerDto(Player player)
        {
            return new RoomPlayerDto
            {
                AccountId = player.Account.Id,
                Nickname = player.Account.Nickname,
                Slot = player.Slot,
                Unk2 = 144
            };
        }

        // RoomCreationOptions -> ChangeRuleDto
        public ChangeRuleDto ToChangeRuleDto(RoomCreationOptions options)
        {
            return new ChangeRuleDto
            {
                GameRule = options.GameRule,
                Map = options.Map,
                PlayerLimit = (byte)options.PlayerLimit,
                ScoreLimit = options.ScoreLimit,
                TimeLimit = options.TimeLimit,
                ItemLimit = options.EquipLimit,
                Password = options.Password,
                Name = options.Name,
                IsSpectatingEnabled = options.IsSpectatingEnabled,
                SpectatorLimit = (byte)options.SpectatorLimit
            };
        }

        // RoomCreationOptions -> ChangeRule2Dto
        public ChangeRule2Dto ToChangeRule2Dto(RoomCreationOptions options)
        {
            var settings = RoomSettings.None;
            if (options.IsFriendly)
                settings |= RoomSettings.IsFriendly;

            return new ChangeRule2Dto
            {
                GameRule = options.GameRule,
                Map = options.Map,
                PlayerLimit = (byte)options.PlayerLimit,
                ScoreLimit = options.ScoreLimit,
                TimeLimit = options.TimeLimit,
                ItemLimit = options.EquipLimit,
                Password = options.Password,
                Name = options.Name,
                IsSpectatingEnabled = options.IsSpectatingEnabled,
                SpectatorLimit = (byte)options.SpectatorLimit,
                Settings = settings
            };
        }

        // ClanMember -> JoinWaiterInfoDto
        public JoinWaiterInfoDto ToJoinWaiterInfoDto(ClanMember member)
        {
            return new JoinWaiterInfoDto
            {
                AccountId = member.AccountId,
                Name = member.Name,
                JoinDate = member.JoinDate,
                Answer1 = member.Answer1,
                Answer2 = member.Answer2,
                Answer3 = member.Answer3,
                Answer4 = member.Answer4,
                Answer5 = member.Answer5
            };
        }

        // ClanMember -> NewMemberInfoDto (auto-mapped same-named props in legacy config)
        public NewMemberInfoDto ToNewMemberInfoDto(ClanMember member)
        {
            return new NewMemberInfoDto
            {
                AccountId = member.AccountId,
                Name = member.Name,
                Role = member.Role,
                JoinDate = member.JoinDate,
                Answer1 = member.Answer1,
                Answer2 = member.Answer2,
                Answer3 = member.Answer3,
                Answer4 = member.Answer4,
                Answer5 = member.Answer5
            };
        }

        // Clan -> ClubSearchResultDto
        public ClubSearchResultDto ToClubSearchResultDto(Clan clan)
        {
            return new ClubSearchResultDto
            {
                Id = (int)clan.Id,
                Icon = clan.Icon,
                Name = clan.Name,
                OwnerName = clan.Owner?.Name,
                Class = clan.Class,
                CreationDate = clan.CreationDate,
                MemberCount = (uint)clan.Count,
                Area = clan.Area,
                Activity = clan.Activity,
                Description = clan.Description
            };
        }
    }
}
