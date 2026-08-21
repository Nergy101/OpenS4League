using OpenS4L.Network.Data.Club;
using OpenS4L.Network.Data.Game;
using OpenS4L.Network.Data.GameRule;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Mappers;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Differential tests for the Game server's object mappings, for the sources that can be
    /// constructed without a live DB/Redis/data-files stack. Each test registers the legacy
    /// ExpressMapper config (<see cref="Legacy.LegacyGameConfig"/>) from a clean slate and
    /// compares its output against the Mapperly <see cref="GameMapper"/> on identical objects.
    /// </summary>
    [Collection("Serial")]
    public class GameMappingDifferentialTests
    {
        private static void Register()
        {
            Legacy.LegacyGameConfig.Register();
        }

        private readonly GameMapper _mapper = GameFixtures.CreateMapper();

        [Fact]
        public void RoomCreationOptions_ToChangeRuleDto_Matches()
        {
            Register();
            var options = GameFixtures.CreateRoomCreationOptions();
            MappingAssert.Equal<ChangeRuleDto>(
                options,
                src => ExpressMapper.Mapper.Map<RoomCreationOptions, ChangeRuleDto>((RoomCreationOptions)src),
                src => _mapper.ToChangeRuleDto((RoomCreationOptions)src));
        }

        [Fact]
        public void RoomCreationOptions_ToChangeRule2Dto_Matches()
        {
            Register();
            var options = GameFixtures.CreateRoomCreationOptions();
            MappingAssert.Equal<ChangeRule2Dto>(
                options,
                src => ExpressMapper.Mapper.Map<RoomCreationOptions, ChangeRule2Dto>((RoomCreationOptions)src),
                src => _mapper.ToChangeRule2Dto((RoomCreationOptions)src));
        }

        [Fact]
        public void PlayerItem_ToItemDto_Matches()
        {
            Register();
            var gds = GameFixtures.CreateGameDataService();
            var item = GameFixtures.CreatePlayerItem(gds);
            MappingAssert.Equal<ItemDto>(
                item,
                src => ExpressMapper.Mapper.Map<PlayerItem, ItemDto>((PlayerItem)src),
                src => _mapper.ToItemDto((PlayerItem)src));
        }
    }
}
