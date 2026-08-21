using System.Linq;
using OpenS4L.Plugins.WebApi.Mappers;
using OpenS4L.Plugins.WebApi.Models;
using OpenS4L.Server.Game.Data;
using OpenS4L.Server.Game.Services;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Differential tests for the WebApi plugin's object mappings, for the sources that can be
    /// constructed without a live DB/Redis stack (MapInfo, ItemInfo, DefaultItem). Each test
    /// registers the legacy ExpressMapper config (<see cref="Legacy.LegacyWebApiConfig"/>) from
    /// a clean slate and compares against the Mapperly <see cref="WebApiMapper"/>.
    /// </summary>
    [Collection("Serial")]
    public class WebApiMappingDifferentialTests
    {
        private static void Register(GameDataService gds)
        {
            Legacy.LegacyWebApiConfig.Register(gds);
        }

        private readonly GameDataService _gds = GameFixtures.CreateGameDataService();
        private readonly WebApiMapper _mapper = new(GameFixtures.Stash.GameDataService);

        [Fact]
        public void MapInfo_ToMapDto_Matches()
        {
            Register(_gds);
            var map = new MapInfo { Id = 3, Name = "Station", GameRule = OpenS4L.GameRule.Practice, PlayerLimit = 8, IsEnabled = true };
            MappingAssert.Equal<MapDto>(
                map,
                src => ExpressMapper.Mapper.Map<MapInfo, MapDto>((MapInfo)src),
                src => _mapper.ToMapDto((MapInfo)src));
        }

        [Fact]
        public void ItemInfo_ToItemDto_Matches()
        {
            Register(_gds);
            var item = GameFixtures.Stash.GameDataService.Items.Values.First();
            MappingAssert.Equal<ItemDto>(
                item,
                src => ExpressMapper.Mapper.Map<ItemInfo, ItemDto>((ItemInfo)src),
                src => _mapper.ToItemDto((ItemInfo)src));
        }

        [Fact]
        public void DefaultItem_ToDefaultItemDto_Matches()
        {
            Register(_gds);
            // Need a DefaultItem whose ItemNumber resolves in _gameDataService.Items.
            var itemNumber = GameFixtures.Stash.GameDataService.Items.Keys.First();
            var defaultItem = new DefaultItem
            {
                ItemNumber = itemNumber,
                Gender = OpenS4L.CharacterGender.Male,
                Variation = 1
            };
            MappingAssert.Equal<DefaultItemDto>(
                defaultItem,
                src => ExpressMapper.Mapper.Map<DefaultItem, DefaultItemDto>((DefaultItem)src),
                src => _mapper.ToDefaultItemDto((DefaultItem)src));
        }
    }
}
