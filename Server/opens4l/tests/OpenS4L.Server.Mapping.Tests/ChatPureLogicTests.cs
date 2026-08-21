using System;
using OpenS4L.Common;
using OpenS4L.Common.Configuration;
using OpenS4L.Server.Chat;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Pure-logic coverage for OpenS4L.Server.Chat types that don't need the network transport:
    /// the player-settings store/converter, AppOptions, and the event-arg bags.
    /// </summary>
    public class ChatPureLogicTests
    {
        private static IdGeneratorService CreateIdGen() =>
            new IdGeneratorService(
                Microsoft.Extensions.Options.Options.Create(new IdGeneratorOptions { Id = 1 }));

        // ---- PlayerSettingManager ----

        [Fact]
        public void PlayerSettingManager_addAndGet_string()
        {
            var mgr = new PlayerSettingManager(CreateIdGen());
            mgr.AddOrUpdate("MySetting", "value");
            Assert.True(mgr.Contains("MySetting"));
            Assert.Equal("value", mgr.Get("MySetting"));
            Assert.Equal("value", mgr.Get<string>("MySetting"));
        }

        [Fact]
        public void PlayerSettingManager_addAndGet_typed()
        {
            var mgr = new PlayerSettingManager(CreateIdGen());
            mgr.AddOrUpdate("MySetting", 42);
            Assert.Equal(42, mgr.Get<int>("MySetting"));
        }

        [Fact]
        public void PlayerSettingManager_addOrUpdate_replacesExisting()
        {
            var mgr = new PlayerSettingManager(CreateIdGen());
            mgr.AddOrUpdate("S", "first");
            mgr.AddOrUpdate("S", "second");
            Assert.Equal("second", mgr.Get("S"));
        }

        [Fact]
        public void PlayerSettingManager_getMissing_throws()
        {
            var mgr = new PlayerSettingManager(CreateIdGen());
            Assert.Throws<Exception>(() => mgr.Get("nope"));
            Assert.Throws<Exception>(() => mgr.Get<int>("nope"));
        }

        [Fact]
        public void PlayerSettingManager_communitySetting_converts()
        {
            // AddOrUpdate stores the RAW string (converters only run during Initialize, which needs
            // a Player + PlayerEntity). So Get<string> returns the string as stored.
            var mgr = new PlayerSettingManager(CreateIdGen());
            mgr.AddOrUpdate(PlayerSetting.AllowFriendRequest.ToString(), CommunitySetting.FriendOnly.ToString());
            Assert.Equal(CommunitySetting.FriendOnly.ToString(), mgr.Get(PlayerSetting.AllowFriendRequest.ToString()));
        }

        [Fact]
        public void PlayerSettingManager_registerConverter_duplicateThrows()
        {
            // The static ctor already registers all four PlayerSetting converters, so re-registering
            // one (under its canonical name) throws.
            Assert.Throws<Exception>(() =>
                PlayerSettingManager.RegisterConverter(
                    PlayerSetting.AllowFriendRequest, new CommunitySettingConverter()));
        }

        [Fact]
        public void CommunitySettingConverter_roundtrips()
        {
            var converter = new CommunitySettingConverter();
            Assert.Equal(CommunitySetting.FriendOnly, converter.GetObject(CommunitySetting.FriendOnly.ToString()));
            Assert.Equal(CommunitySetting.FriendOnly.ToString(), converter.GetString(CommunitySetting.FriendOnly));
            Assert.Throws<Exception>(() => converter.GetObject("NotASetting"));
        }

        // ---- AppOptions ----

        [Fact]
        public void AppOptions_instantiates()
        {
            var o = new AppOptions
            {
                Network = new NetworkOptions(),
                ServerList = new ServerListOptions { Id = 1 },
                Database = new DatabaseOptions(),
                Logging = new LoggerOptions()
            };
            Assert.Equal(1, o.ServerList.Id);
            Assert.NotNull(o.Network);
            Assert.NotNull(o.Database);
            Assert.NotNull(o.Logging);
        }

        // ---- Event args ----

        [Fact]
        public void ChannelEventArgs_holdsChannelAndPlayer()
        {
            var channel = new Channel(1, null, new OpenS4L.Server.Chat.Mappers.ChatMapper(1));
            var player = ChatFixtures.CreatePlayer(1);
            var args = new ChannelEventArgs(channel, player);
            Assert.Equal(channel, args.Channel);
            Assert.Equal(player, args.Player);
        }

        [Fact]
        public void PlayerEventArgs_holdsPlayer()
        {
            var player = ChatFixtures.CreatePlayer(1);
            var args = new PlayerEventArgs(player);
            Assert.Equal(player, args.Player);
        }
    }
}
