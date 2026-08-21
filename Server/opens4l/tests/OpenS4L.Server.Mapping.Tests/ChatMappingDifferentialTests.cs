using OpenS4L.Network.Data.Chat;
using OpenS4L.Server.Chat;
using OpenS4L.Server.Chat.Mappers;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Differential tests for the Chat server's object mappings. Each test registers the legacy
    /// ExpressMapper config (<see cref="Legacy.LegacyChatConfig"/>) from a clean slate and compares
    /// its output against the Mapperly <see cref="ChatMapper"/> on identical source objects.
    /// </summary>
    [Collection("Serial")]
    public class ChatMappingDifferentialTests
    {
        private readonly ChatMapper _mapper = ChatFixtures.CreateMapper();

        /// <summary>Registers Chat's legacy ExpressMapper config from a clean slate.</summary>
        private static void Register()
        {
            Legacy.LegacyChatConfig.Register(3);
        }

        [Fact]
        public void Mail_ToNoteDto_Matches()
        {
            Register();
            var mail = ChatFixtures.CreateMail();
            MappingAssert.Equal<NoteDto>(
                mail,
                src => ExpressMapper.Mapper.Map<Mail, NoteDto>((Mail)src),
                src => _mapper.ToNoteDto((Mail)src));
        }

        [Fact]
        public void Mail_ToNoteDto_IsNew_Matches()
        {
            Register();
            var mail = ChatFixtures.CreateMail(isNew: true);
            MappingAssert.Equal<NoteDto>(
                mail,
                src => ExpressMapper.Mapper.Map<Mail, NoteDto>((Mail)src),
                src => _mapper.ToNoteDto((Mail)src));
        }

        [Fact]
        public void Mail_ToNoteContentDto_Matches()
        {
            Register();
            var mail = ChatFixtures.CreateMail();
            MappingAssert.Equal<NoteContentDto>(
                mail,
                src => ExpressMapper.Mapper.Map<Mail, NoteContentDto>((Mail)src),
                src => _mapper.ToNoteContentDto((Mail)src));
        }

        [Fact]
        public void Deny_ToDenyDto_Matches()
        {
            Register();
            var deny = ChatFixtures.CreateDeny();
            MappingAssert.Equal<DenyDto>(
                deny,
                src => ExpressMapper.Mapper.Map<Deny, DenyDto>((Deny)src),
                src => _mapper.ToDenyDto((Deny)src));
        }

        [Fact]
        public void Friend_ToFriendDto_Matches()
        {
            Register();
            var friend = ChatFixtures.CreateFriend();
            MappingAssert.Equal<FriendDto>(
                friend,
                src => ExpressMapper.Mapper.Map<Friend, FriendDto>((Friend)src),
                src => _mapper.ToFriendDto((Friend)src));
        }

        [Fact]
        public void Player_ToPlayerInfoShortDto_Matches()
        {
            Register();
            var player = ChatFixtures.CreatePlayer(3);
            MappingAssert.Equal<PlayerInfoShortDto>(
                player,
                src => ExpressMapper.Mapper.Map<Player, PlayerInfoShortDto>((Player)src),
                src => _mapper.ToPlayerInfoShortDto((Player)src));
        }

        [Fact]
        public void Player_ToUserDataDto_Matches()
        {
            Register();
            var player = ChatFixtures.CreatePlayer(3);
            MappingAssert.Equal<UserDataDto>(
                player,
                src => ExpressMapper.Mapper.Map<Player, UserDataDto>((Player)src),
                src => _mapper.ToUserDataDto((Player)src));
        }

        [Fact]
        public void Player_ToPlayerLocationDto_Matches()
        {
            Register();
            var player = ChatFixtures.CreatePlayer(3);
            MappingAssert.Equal<PlayerLocationDto>(
                player,
                src => ExpressMapper.Mapper.Map<Player, PlayerLocationDto>((Player)src),
                src => _mapper.ToPlayerLocationDto((Player)src));
        }

        [Fact]
        public void Player_ToPlayerInfoDto_Matches()
        {
            Register();
            var player = ChatFixtures.CreatePlayer(3);
            MappingAssert.Equal<PlayerInfoDto>(
                player,
                src => ExpressMapper.Mapper.Map<Player, PlayerInfoDto>((Player)src),
                src => _mapper.ToPlayerInfoDto((Player)src));
        }

        [Fact]
        public void ClanMemberInfo_ToClubMemberDto_Matches()
        {
            Register();
            var member = ChatFixtures.CreateClanMemberInfo();
            MappingAssert.Equal<ClubMemberDto>(
                member,
                src => ExpressMapper.Mapper.Map<OpenS4L.Common.Messaging.ClanMemberInfo, ClubMemberDto>(
                    (OpenS4L.Common.Messaging.ClanMemberInfo)src),
                src => _mapper.ToClubMemberDto((OpenS4L.Common.Messaging.ClanMemberInfo)src));
        }
    }
}
