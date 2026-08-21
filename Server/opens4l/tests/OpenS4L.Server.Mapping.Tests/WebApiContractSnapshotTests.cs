using System.Linq;
using System.Threading.Tasks;
using OpenS4L.Plugins.WebApi.Mappers;
using OpenS4L.Plugins.WebApi.Models;
using OpenS4L.Server.Game.Data;
using OpenS4L.Server.Game.Services;
using VerifyXunit;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Snapshot tests pinning the WebApi contract: the exact JSON the HTTP endpoints expose for
    /// representative DTOs. A change here (a renamed field, a dropped member, a different type)
    /// will surface as a snapshot diff, guarding against accidental API breakage.
    /// </summary>
    public class WebApiContractSnapshotTests
    {
        private readonly GameDataService _gds = GameFixtures.CreateGameDataService();
        private readonly WebApiMapper _mapper = new(GameFixtures.Stash.GameDataService);

        [Fact]
        public Task MapInfo_contract_is_stable()
        {
            var map = new MapInfo
            {
                Id = 3,
                Name = "Station",
                GameRule = OpenS4L.GameRule.Practice,
                PlayerLimit = 8,
                IsEnabled = true
            };
            return Verifier.Verify(_mapper.ToMapDto(map));
        }

        [Fact]
        public Task ItemInfo_contract_is_stable()
        {
            var item = _gds.Items.Values.First();
            return Verifier.Verify(_mapper.ToItemDto(item));
        }

        [Fact]
        public Task DefaultItem_contract_is_stable()
        {
            var itemNumber = _gds.Items.Keys.First();
            var defaultItem = new DefaultItem
            {
                ItemNumber = itemNumber,
                Gender = OpenS4L.CharacterGender.Male,
                Variation = 1
            };
            return Verifier.Verify(_mapper.ToDefaultItemDto(defaultItem));
        }
    }
}
