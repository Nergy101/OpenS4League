using ExpressMapper;
using ExpressMapper.Extensions;
using OpenS4L.Common.Messaging;
using OpenS4L.Network.Data.Chat;
using OpenS4L.Server.Chat;

namespace OpenS4L.Server.Mapping.Tests.Legacy
{
    /// <summary>
    /// The Chat server's legacy ExpressMapper config, preserved verbatim from
    /// <c>OpenS4L.Server.Chat.Program.ConfigureMapper</c>. Used by the differential tests as
    /// the "old behaviour" baseline now that production has migrated to Mapperly.
    /// </summary>
    internal static class LegacyChatConfig
    {
        public static void Register(ushort serverGroupId)
        {
            Mapper.Reset();
            var appOptions = new AppOptions
            {
                ServerList = new OpenS4L.Common.Configuration.ServerListOptions { Id = serverGroupId }
            };

            Mapper.Register<Mail, NoteDto>()
                .Function(dest => dest.ReadCount, src => src.IsNew ? 0 : 1)
                .Function(dest => dest.DaysLeft,
                    src => System.DateTimeOffset.Now < src.Expires
                        ? (src.Expires - System.DateTimeOffset.Now).TotalDays
                        : 0);

            Mapper.Register<Mail, NoteContentDto>()
                .Member(dest => dest.Id, src => src.Id)
                .Member(dest => dest.Message, src => src.Message);

            Mapper.Register<Deny, DenyDto>()
                .Member(dest => dest.AccountId, src => src.DenyId)
                .Member(dest => dest.Nickname, src => src.Nickname);

            Mapper.Register<Friend, FriendDto>()
                .Member(dest => dest.AccountId, src => src.FriendId)
                .Member(dest => dest.Nickname, src => src.Nickname)
                .Member(dest => dest.State, src => src.State);

            Mapper.Register<Player, PlayerInfoShortDto>()
                .Member(dest => dest.AccountId, src => src.Account.Id)
                .Member(dest => dest.Nickname, src => src.Account.Nickname);

            Mapper.Register<Player, UserDataDto>()
                .Member(dest => dest.Nickname, src => src.Account.Nickname)
                .Member(dest => dest.AccountId, src => src.Account.Id);

            Mapper.Register<Player, PlayerLocationDto>()
                .Function(dest => dest.ServerGroupId, src => appOptions.ServerList.Id)
                .Function(dest => dest.GameServerId, src => appOptions.ServerList.Id << 8 | (byte)ServerType.Game)
                .Function(dest => dest.ChatServerId, src => appOptions.ServerList.Id << 8 | (byte)ServerType.Chat)
                .Function(dest => dest.ChannelId, src => src.Channel == null ? -1 : (int)src.Channel.Id)
                .Function(dest => dest.RoomId, src => src.RoomId == 0 ? -1 : (int)src.RoomId)
                .Member(dest => dest.ClanId, src => src.ClanId);

            Mapper.Register<Player, PlayerInfoDto>()
                .Function(dest => dest.Info, src => src.Map<Player, PlayerInfoShortDto>())
                .Function(dest => dest.Location, src => src.Map<Player, PlayerLocationDto>());

            Mapper.Register<ClanMemberInfo, ClubMemberDto>();

            Mapper.Compile(CompilationTypes.Source);
        }
    }
}
