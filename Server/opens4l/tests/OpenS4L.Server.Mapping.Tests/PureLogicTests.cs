using System;
using System.Collections.Immutable;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Server.Game.Data;
using OpenS4L.Server.Game.Services;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Pure-logic tests for small value/helper types that are easy to break and hard to notice:
    /// the id recycler, the item-number encoding, and the level-from-experience lookup.
    /// </summary>
    public class PureLogicTests
    {
        [Fact]
        public void CounterRecycler_issuesIncreasingIds()
        {
            var recycler = new CounterRecycler();
            Assert.Equal(1u, recycler.GetId());
            Assert.Equal(2u, recycler.GetId());
            Assert.Equal(3u, recycler.GetId());
        }

        [Fact]
        public void CounterRecycler_reusesReturnedIds()
        {
            var recycler = new CounterRecycler();
            Assert.Equal(1u, recycler.GetId());
            Assert.Equal(2u, recycler.GetId());
            recycler.Return(2);
            Assert.Equal(2u, recycler.GetId());
            Assert.Equal(3u, recycler.GetId());
        }

        [Fact]
        public void ItemNumber_roundtripsIdToComponents()
        {
            var number = new ItemNumber(ItemCategory.Weapon, 3, 42);
            Assert.Equal(ItemCategory.Weapon, number.Category);
            Assert.Equal((byte)3, number.SubCategory);
            Assert.Equal((ushort)42, number.Number);

            // Reconstruct from the packed id and assert components survive.
            var fromId = new ItemNumber(number.Id);
            Assert.Equal(number.Id, fromId.Id);
            Assert.Equal(ItemCategory.Weapon, fromId.Category);
            Assert.Equal((byte)3, fromId.SubCategory);
            Assert.Equal((ushort)42, fromId.Number);
        }

        [Fact]
        public void ItemNumber_knownEncoding()
        {
            // 2_000_001 => category 2 (Weapon), sub 0, number 1.
            var number = new ItemNumber(2000001u);
            Assert.Equal(ItemCategory.Weapon, number.Category);
            Assert.Equal((byte)0, number.SubCategory);
            Assert.Equal((ushort)1, number.Number);
        }

        [Fact]
        public void GetLevelFromExperience_returnsHighestMatchingLevel()
        {
            var gds = GameFixtures.CreateGameDataService();
            SetLevels(gds, new[]
            {
                new LevelInfo { Level = 0, ExperienceToNextLevel = 100, TotalExperience = 0 },
                new LevelInfo { Level = 1, ExperienceToNextLevel = 200, TotalExperience = 100 },
                new LevelInfo { Level = 2, ExperienceToNextLevel = 300, TotalExperience = 300 },
                new LevelInfo { Level = 3, ExperienceToNextLevel = 400, TotalExperience = 600 },
            });

            Assert.Equal(0, gds.GetLevelFromExperience(0).Level);
            Assert.Equal(1, gds.GetLevelFromExperience(100).Level);
            Assert.Equal(2, gds.GetLevelFromExperience(300).Level);
            Assert.Equal(3, gds.GetLevelFromExperience(600).Level);
            Assert.Equal(3, gds.GetLevelFromExperience(99999).Level); // past max => last level
        }

        private static void SetLevels(GameDataService gds, LevelInfo[] levels)
        {
            var dict = levels.ToImmutableDictionary(x => x.Level, x => x);
            typeof(GameDataService)
                .GetProperty(nameof(GameDataService.Levels),
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                !.SetValue(gds, dict);
        }
    }
}
