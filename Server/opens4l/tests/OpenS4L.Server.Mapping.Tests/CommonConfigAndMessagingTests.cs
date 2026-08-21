using System;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Common.Configuration;
using OpenS4L.Common.Messaging;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Coverage for OpenS4L.Common config option POCOs and messaging DTOs. These are simple
    /// property bags (bound from hjson / serialized over the message bus), so exercising their
    /// ctors and property setters covers them.
    /// </summary>
    public class CommonConfigAndMessagingTests
    {
        [Fact]
        public void GameOptions_instantiates()
        {
            var o = new GameOptions
            {
                EnableTutorial = true,
                MaxLevel = 99,
                StartLevel = 1,
                StartPEN = 1000,
                StartAP = 0,
                StartCoins1 = 500,
                StartCoins2 = 100,
                DurabilityLossPerDeath = 1,
                DurabilityLossPerMinute = 2,
                NickRestrictions = new NickRestrictionOptions(),
                ClanOptions = new ClanOptions(),
                Deathmatch = new DeathmatchOptions { PointsPerKill = 1 },
                Touchdown = new TouchdownOptions { PointsPerGoal = 5 },
                BattleRoyal = new BattleRoyalOptions { PointsPerKill = 2 },
                Captain = new CaptainOptions { PointsPerRoundWin = 10 }
            };
            Assert.True(o.EnableTutorial);
            Assert.Equal(99, o.MaxLevel);
            Assert.Equal(5, o.Touchdown.PointsPerGoal);
            Assert.NotNull(o.Deathmatch.ExperienceRates = new ExperienceRateOptions { ScoreFactor = 1.0f });
            Assert.Equal(1.0f, o.Deathmatch.ExperienceRates.ScoreFactor);
        }

        [Fact]
        public void ClanOptions_instantiates()
        {
            var o = new ClanOptions { NameMinLength = 3, NameMaxLength = 12, DefaultIcon = "icon" };
            Assert.Equal(3, o.NameMinLength);
            Assert.Equal("icon", o.DefaultIcon);
        }

        [Fact]
        public void NickRestrictionOptions_instantiates()
        {
            var o = new NickRestrictionOptions { MinLength = 3, MaxLength = 16, MaxRepeat = 2, WhitespaceAllowed = false, AsciiOnly = true };
            Assert.Equal(16, o.MaxLength);
            Assert.True(o.AsciiOnly);
        }

        [Fact]
        public void ChatLogin_requestAndResponse()
        {
            var req = new ChatLoginRequest(123, "session");
            Assert.Equal(123UL, req.AccountId);
            Assert.Equal("session", req.SessionId);

            var resp = new ChatLoginResponse(true, new Account(1, "u", "n", SecurityLevel.User), 100, 7);
            Assert.True(resp.OK);
            Assert.Equal((uint)7, resp.ClanId);
            // parameterless ctors
            var req2 = new ChatLoginRequest();
            var resp2 = new ChatLoginResponse();
            Assert.NotNull(req2);
            Assert.NotNull(resp2);
        }

        [Fact]
        public void ClanMemberList_requestAndResponse()
        {
            var req = new ClanMemberListRequest(5);
            Assert.Equal((uint)5, req.ClanId);
            var resp = new ClanMemberListResponse(new[] { new ClanMemberInfo { AccountId = 1 } });
            Assert.Single(resp.Members);
            var req2 = new ClanMemberListRequest();
            var resp2 = new ClanMemberListResponse();
            Assert.NotNull(req2);
            Assert.NotNull(resp2);
        }

        [Fact]
        public void LevelFromExperience_requestAndResponse()
        {
            var req = new LevelFromExperienceRequest(5000);
            Assert.Equal((uint)5000, req.TotalExperience);
            var resp = new LevelFromExperienceResponse(12);
            Assert.Equal(12, resp.Level);
            var req2 = new LevelFromExperienceRequest();
            var resp2 = new LevelFromExperienceResponse();
            Assert.NotNull(req2);
            Assert.NotNull(resp2);
        }

        [Fact]
        public void RelayLogin_requestAndResponse()
        {
            var req = new RelayLoginRequest { AccountId = 9, ServerId = 1, ChannelId = 2, RoomId = 3 };
            Assert.Equal(9UL, req.AccountId);
            var resp = new RelayLoginResponse(true, new Account(1, "u", "n", SecurityLevel.User));
            Assert.True(resp.OK);
            var req2 = new RelayLoginRequest();
            var resp2 = new RelayLoginResponse();
            Assert.NotNull(req2);
            Assert.NotNull(resp2);
        }

        [Fact]
        public void MiscMessages_instantiate()
        {
            var joined = new ChannelPlayerJoinedMessage(1, 2);
            var left = new ChannelPlayerLeftMessage(1, 2);
            var update = new PlayerUpdateMessage(1, 10, 2, 3, TeamId.Alpha);
            Assert.Equal(TeamId.Alpha, update.TeamId);
            var disconnect = new PlayerDisconnectedMessage(5);
            var peer = new PlayerPeerIdMessage(5, new PeerId(1, 1, 1));
            var shutdown = new ServerShutdownMessage { Id = 1, ServerType = ServerType.Game };
            Assert.Equal(ServerType.Game, shutdown.ServerType);
            var serverUpdate = new ServerUpdateMessage
            {
                Id = 2, ServerType = ServerType.Chat, Name = "srv",
                Online = 10, Limit = 100,
                EndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 22000)
            };
            Assert.Equal(100, serverUpdate.Limit);
            var clanUpdate = new ClanMemberUpdateMessage(3, 4, ClubMemberPresenceState.Online, true);
            Assert.True(clanUpdate.LoggedIn);
            var clanUpdate2 = new ClanMemberUpdateMessage(3, 4, ClubMemberPresenceState.Offline);
            Assert.False(clanUpdate2.LoggedIn);
            Assert.NotNull(joined);
            Assert.NotNull(left);
            Assert.NotNull(disconnect);
            Assert.NotNull(peer);
        }
    }
}
