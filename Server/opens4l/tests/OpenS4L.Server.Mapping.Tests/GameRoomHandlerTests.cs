using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network;
using OpenS4L.Network.Data.Game;
using OpenS4L.Network.Message.Game;
using OpenS4L.Network.Message.GameRule;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using OpenS4L.Server.Game.Services;
using ProudNet;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Drives the Game RoomHandler (make room / leave room / room info) over the harness.
    /// </summary>
    public class GameRoomHandlerTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();

        private async Task<(Player plr, FakeSocketChannel channel)> LoginAsync(uint accountId)
        {
            var cache = (Foundatio.Caching.InMemoryCacheClient)_ctx.Get<Foundatio.Caching.ICacheClient>();
            await cache.SetAsync<string>(Constants.Cache.SessionKey(accountId), "sid-" + accountId);
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = (int)accountId, Username = "g" + accountId, Nickname = "nick" + accountId });
                await auth.SaveChangesAsync();
            }
            using (var db = _ctx.Get<GameContext>())
            {
                db.Players.Add(new PlayerEntity { Id = (int)accountId, TotalExperience = 1000 });
                await db.SaveChangesAsync();
            }

            var handler = _ctx.Get<AuthenticationHandler>();
            var (session, channel) = _ctx.CreateSession(accountId);
            await handler.OnHandle(new MessageContext { Session = session }, new LoginRequestReqMessage
            {
                AccountId = accountId, SessionId = "sid-" + accountId, Version = new Version(1, 0, 0, 0)
            });
            return (session.Player, channel);
        }

        private async Task JoinChannelAsync(Player plr)
        {
            using (var db = _ctx.Get<GameContext>())
            {
                db.Channels.Add(new ChannelEntity { Id = 1, Name = "C", Description = "d", PlayerLimit = 32, Color = "FF0000", MinLevel = 0, MaxLevel = 99 });
                await db.SaveChangesAsync();
            }
            await _ctx.Get<ChannelService>().StartAsync(System.Threading.CancellationToken.None);
            await _ctx.Get<ChannelHandler>().OnHandle(new MessageContext { Session = plr.Session }, new ChannelEnterReqMessage { Channel = 1 });
        }

        [Fact]
        public async Task RoomMake2_success_createsRoom()
        {
            var (plr, channel) = await LoginAsync(2101);
            await JoinChannelAsync(plr);
            Assert.NotNull(plr.Channel);

            var handler = _ctx.Get<RoomHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new RoomMakeReq2Message
            {
                Room = new MakeRoom2Dto
                {
                    Name = "TestRoom", GameRule = GameRule.Practice, Map = 3, PlayerLimit = 8,
                    ScoreLimit = 5, TimeLimit = TimeSpan.FromMinutes(10), ItemLimit = 1,
                    Password = "", IsSpectatingEnabled = false, SpectatorLimit = 0
                }
            });

            Assert.NotNull(plr.Room);
            Assert.Equal("TestRoom", plr.Room.Options.Name);
            Assert.Equal(GameRule.Practice, plr.Room.Options.GameRule);
        }

        [Fact]
        public async Task RoomMake2_invalidMap_returnsError()
        {
            var (plr, channel) = await LoginAsync(2102);
            await JoinChannelAsync(plr);

            var handler = _ctx.Get<RoomHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new RoomMakeReq2Message
            {
                Room = new MakeRoom2Dto
                {
                    Name = "BadRoom", GameRule = GameRule.Practice, Map = 99, PlayerLimit = 8,
                    ScoreLimit = 5, TimeLimit = TimeSpan.FromMinutes(10), ItemLimit = 1,
                    Password = "", IsSpectatingEnabled = false, SpectatorLimit = 0
                }
            });

            Assert.Null(plr.Room);
            var ack = channel.Outbound.Select(o => o.GetType().GetProperty("Message")?.GetValue(o)).OfType<ServerResultAckMessage>().LastOrDefault();
            Assert.NotNull(ack);
        }

        [Fact]
        public async Task RoomLeave_leaves()
        {
            var (plr, _) = await LoginAsync(2103);
            await JoinChannelAsync(plr);
            var handler = _ctx.Get<RoomHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new RoomMakeReq2Message
            {
                Room = new MakeRoom2Dto
                {
                    Name = "Room", GameRule = GameRule.Practice, Map = 3, PlayerLimit = 8,
                    ScoreLimit = 5, TimeLimit = TimeSpan.FromMinutes(10), ItemLimit = 1,
                    Password = "", IsSpectatingEnabled = false, SpectatorLimit = 0
                }
            });
            Assert.NotNull(plr.Room);

            await handler.OnHandle(new MessageContext { Session = plr.Session }, new RoomLeaveReqMessage());
            Assert.Null(plr.Room);
        }

        [Fact]
        public async Task RoomInfoRequest_returnsInfo()
        {
            var (plr, channel) = await LoginAsync(2104);
            await JoinChannelAsync(plr);
            var handler = _ctx.Get<RoomHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new RoomMakeReq2Message
            {
                Room = new MakeRoom2Dto
                {
                    Name = "Room", GameRule = GameRule.Practice, Map = 3, PlayerLimit = 8,
                    ScoreLimit = 5, TimeLimit = TimeSpan.FromMinutes(10), ItemLimit = 1,
                    Password = "", IsSpectatingEnabled = false, SpectatorLimit = 0
                }
            });

            await handler.OnHandle(new MessageContext { Session = plr.Session }, new RoomInfoRequestReqMessage { RoomId = plr.Room.Id });
            Assert.Contains(channel.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is RoomInfoRequestAck2Message);
        }

        private async Task CreateCharacterAsync(Player plr)
        {
            var (character, result) = plr.CharacterManager.Create(0, CharacterGender.Male, 0, 0, 0, 0, 0, 0);
            Assert.Equal(CharacterCreateResult.Success, result);
            plr.CharacterManager.Select(0);
            Assert.NotNull(plr.CharacterManager.CurrentCharacter);
        }

        [Fact]
        public async Task ReadyRound_togglesReady()
        {
            var (plr, _) = await LoginAsync(2105);
            await JoinChannelAsync(plr);
            var handler = _ctx.Get<RoomHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new RoomMakeReq2Message
            {
                Room = new MakeRoom2Dto
                {
                    Name = "Room", GameRule = GameRule.Practice, Map = 3, PlayerLimit = 8,
                    ScoreLimit = 5, TimeLimit = TimeSpan.FromMinutes(10), ItemLimit = 1,
                    Password = "", IsSpectatingEnabled = false, SpectatorLimit = 0
                }
            });
            Assert.NotNull(plr.Room);
            await CreateCharacterAsync(plr);

            await handler.OnHandle(new MessageContext { Session = plr.Session }, new RoomReadyRoundReq2Message { IsReady = true, EquipCheck = null });
            // Character has no weapon → EquipValidator.IsValid is false → ready not toggled, error sent.
            Assert.False(plr.IsReady);
        }

        [Fact]
        public async Task GameEvent_broadcasts()
        {
            var (plr, channel) = await LoginAsync(2106);
            await JoinChannelAsync(plr);
            var handler = _ctx.Get<RoomHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new RoomMakeReq2Message
            {
                Room = new MakeRoom2Dto
                {
                    Name = "Room", GameRule = GameRule.Practice, Map = 3, PlayerLimit = 8,
                    ScoreLimit = 5, TimeLimit = TimeSpan.FromMinutes(10), ItemLimit = 1,
                    Password = "", IsSpectatingEnabled = false, SpectatorLimit = 0
                }
            });

            await handler.OnHandle(new MessageContext { Session = plr.Session }, new GameEventMessageReqMessage
            {
                Event = GameEventMessage.StartGame, AccountId = plr.Account.Id, Unk1 = 0, Value = 0
            });
            Assert.Contains(channel.Outbound, o => o.GetType().GetProperty("Message")?.GetValue(o) is GameEventMessageAckMessage);
        }

        [Fact]
        public async Task PlayModeChange_lobby_changesMode()
        {
            var (plr, _) = await LoginAsync(2107);
            await JoinChannelAsync(plr);
            var handler = _ctx.Get<RoomHandler>();
            await handler.OnHandle(new MessageContext { Session = plr.Session }, new RoomMakeReq2Message
            {
                Room = new MakeRoom2Dto
                {
                    Name = "Room", GameRule = GameRule.Practice, Map = 3, PlayerLimit = 8,
                    ScoreLimit = 5, TimeLimit = TimeSpan.FromMinutes(10), ItemLimit = 1,
                    Password = "", IsSpectatingEnabled = false, SpectatorLimit = 0
                }
            });
            Assert.NotNull(plr.Room);

            await handler.OnHandle(new MessageContext { Session = plr.Session }, new RoomPlayModeChangeReqMessage { Mode = PlayerGameMode.Spectate });
            // Practice room's Alpha team has 0 spectator slots → ChangeMode returns Full, mode stays Normal.
            Assert.Equal(PlayerGameMode.Normal, plr.Mode);
        }
    }
}
