using OpenS4L;
using OpenS4L.Plugins.WebApi;
using OpenS4L.Plugins.WebApi.Models;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Coverage for the WebApi DTOs and options. These are the JSON contract types exposed by the
    /// HTTP API — plain property bags, so exercising ctors + property setters covers them.
    /// </summary>
    public class WebApiDtoTests
    {
        [Fact]
        public void WebApiOptions_holdsListener()
        {
            var o = new WebApiOptions { Listener = "http://0.0.0.0:22000/" };
            Assert.Equal("http://0.0.0.0:22000/", o.Listener);
        }

        [Fact]
        public void PlayerDto_roundtrips()
        {
            var dto = new PlayerDto
            {
                Id = 1,
                Username = "user",
                Nickname = "nick",
                Level = 5,
                TotalExperience = 1000,
                PEN = 50,
                AP = 20,
                ActiveCharacter = 1,
                Characters = new CharacterDto[0],
                Inventory = new PlayerItemDto[0],
                ChannelId = 3,
                RoomId = 4
            };
            Assert.Equal(1UL, dto.Id);
            Assert.Equal("nick", dto.Nickname);
            Assert.Equal(5, dto.Level);
            Assert.Equal((uint?)3, dto.ChannelId);
            Assert.Equal((uint?)4, dto.RoomId);
        }

        [Fact]
        public void CharacterDto_roundtrips()
        {
            var dto = new CharacterDto
            {
                Id = 9, Slot = 2, Gender = CharacterGender.Female,
                Hair = new DefaultItemDto(), Face = new DefaultItemDto(),
                Shirt = new DefaultItemDto(), Pants = new DefaultItemDto(),
                Gloves = new DefaultItemDto(), Shoes = new DefaultItemDto(),
                Weapons = new ulong[] { 1, 2 }, Skills = new ulong[] { 3 }, Costumes = new ulong[] { 4 }
            };
            Assert.Equal(CharacterGender.Female, dto.Gender);
            Assert.Equal(new ulong[] { 1, 2 }, dto.Weapons);
        }

        [Fact]
        public void PlayerItemDto_roundtrips()
        {
            var dto = new PlayerItemDto
            {
                Id = 1, Item = new ItemDto(), PriceType = ItemPriceType.PEN,
                PeriodType = ItemPeriodType.Days, Period = 30, Color = 2,
                Effect = 1, PurchaseTimestamp = 1000, Durability = 5, Count = 1
            };
            Assert.Equal(ItemPeriodType.Days, dto.PeriodType);
            Assert.Equal(30, dto.Period);
            Assert.Equal(1000L, dto.PurchaseTimestamp);
        }

        [Fact]
        public void ChannelDto_roundtrips()
        {
            var dto = new ChannelDto
            {
                Id = 1, Category = ChannelCategory.Speed, Name = "Channel 1",
                PlayerLimit = 16, Type = 0, PlayersOnline = 4
            };
            Assert.Equal(ChannelCategory.Speed, dto.Category);
            Assert.Equal(16, dto.PlayerLimit);
            Assert.Equal(4, dto.PlayersOnline);
        }

        [Fact]
        public void RoomDto_roundtrips()
        {
            var dto = new RoomDto
            {
                Id = 5, Name = "Room", CreationTimestamp = 100, MasterId = 1, HostId = 2,
                Map = new MapDto(), GameRule = GameRule.Practice, State = GameState.Waiting,
                TimeState = GameTimeState.FirstHalf, PlayerLimit = 8, SpectatorLimit = 2,
                Password = "", TimeLimit = 10, ScoreLimit = 50, IsFriendly = true, EquipLimit = 3,
                Players = new RoomPlayerDto[0]
            };
            Assert.Equal(GameRule.Practice, dto.GameRule);
            Assert.Equal(GameState.Waiting, dto.State);
            Assert.Equal(8, dto.PlayerLimit);
            Assert.True(dto.IsFriendly);
        }

        [Fact]
        public void RoomPlayerDto_inheritsPlayerDto()
        {
            var dto = new RoomPlayerDto { TeamId = TeamId.Alpha, Id = 7 };
            Assert.Equal(TeamId.Alpha, dto.TeamId);
            Assert.Equal(7UL, dto.Id);
        }

        [Fact]
        public void StatisticsDto_bothCtors()
        {
            var dto = new StatisticsDto(100, 5);
            Assert.Equal(100L, dto.Uptime);
            Assert.Equal(5, dto.PlayersOnline);
            var empty = new StatisticsDto();
            Assert.Equal(0L, empty.Uptime);
        }

        [Fact]
        public void RequestDtos_roundtrip()
        {
            var ban = new BanRequestDto { PlayerId = 1, Duration = 60, Reason = "spam" };
            Assert.Equal(60L, ban.Duration);
            var kick = new RoomKickRequestDto { PlayerId = 2, Reason = RoomLeaveReason.Kicked };
            Assert.Equal(RoomLeaveReason.Kicked, kick.Reason);
            var close = new CloseRoomRequestDto { ChannelId = 3, RoomId = 4 };
            Assert.Equal((uint)4, close.RoomId);
        }
    }
}
