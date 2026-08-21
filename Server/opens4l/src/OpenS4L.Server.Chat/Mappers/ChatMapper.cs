using System;
using OpenS4L.Common;
using OpenS4L.Network.Data.Chat;
using Riok.Mapperly.Abstractions;

namespace OpenS4L.Server.Chat.Mappers
{
    /// <summary>
    /// Mapperly replacement for the ExpressMapper registrations previously in
    /// <c>OpenS4L.Server.Chat.Program.ConfigureMapper</c>. Produces byte-for-byte
    /// the same DTOs as the legacy config (verified by the differential tests in
    /// <c>OpenS4L.Server.Mapping.Tests</c>).
    /// </summary>
    [Mapper]
    public partial class ChatMapper
    {
        private readonly ushort _serverGroupId;

        public ChatMapper(ushort serverGroupId)
        {
            _serverGroupId = serverGroupId;
        }

        // Mail -> NoteDto
        public NoteDto ToNoteDto(Mail mail)
        {
            return new NoteDto
            {
                Id = (ulong)mail.Id,
                Sender = mail.Sender,
                Title = mail.Title,
                ReadCount = (uint)(mail.IsNew ? 0 : 1),
                DaysLeft = (byte)(DateTimeOffset.Now < mail.Expires
                    ? (mail.Expires - DateTimeOffset.Now).TotalDays
                    : 0)
            };
        }

        // Mail -> NoteContentDto
        public NoteContentDto ToNoteContentDto(Mail mail)
        {
            return new NoteContentDto
            {
                Id = (ulong)mail.Id,
                Message = mail.Message
            };
        }

        // Deny -> DenyDto
        public DenyDto ToDenyDto(Deny deny)
        {
            return new DenyDto
            {
                AccountId = deny.DenyId,
                Nickname = deny.Nickname
            };
        }

        // Friend -> FriendDto
        public FriendDto ToFriendDto(Friend friend)
        {
            return new FriendDto
            {
                AccountId = friend.FriendId,
                Nickname = friend.Nickname,
                State = friend.State
            };
        }

        // Player -> PlayerInfoShortDto
        public PlayerInfoShortDto ToPlayerInfoShortDto(Player player)
        {
            return new PlayerInfoShortDto
            {
                AccountId = player.Account.Id,
                Nickname = player.Account.Nickname,
                TotalExperience = player.TotalExperience
            };
        }

        // Player -> UserDataDto
        public UserDataDto ToUserDataDto(Player player)
        {
            return new UserDataDto
            {
                Nickname = player.Account.Nickname,
                AccountId = player.Account.Id,
                TotalExperience = player.TotalExperience,
                Level = (uint)player.Level
            };
        }

        // Player -> PlayerLocationDto
        public PlayerLocationDto ToPlayerLocationDto(Player player)
        {
            return new PlayerLocationDto
            {
                ServerGroupId = _serverGroupId,
                GameServerId = _serverGroupId << 8 | (byte)ServerType.Game,
                ChatServerId = _serverGroupId << 8 | (byte)ServerType.Chat,
                ChannelId = player.Channel == null ? -1 : (int)player.Channel.Id,
                RoomId = player.RoomId == 0 ? -1 : (int)player.RoomId,
                ClanId = (int)player.ClanId
            };
        }

        // Player -> PlayerInfoDto
        public PlayerInfoDto ToPlayerInfoDto(Player player)
        {
            return new PlayerInfoDto(ToPlayerInfoShortDto(player), ToPlayerLocationDto(player));
        }

        // ClanMemberInfo -> ClubMemberDto
        public ClubMemberDto ToClubMemberDto(OpenS4L.Common.Messaging.ClanMemberInfo member)
        {
            return new ClubMemberDto
            {
                AccountId = member.AccountId,
                Nickname = member.Nickname,
                Role = member.Role,
                LastLoginDate = member.LastLoginDate,
                PresenceState = member.PresenceState
            };
        }
    }
}
