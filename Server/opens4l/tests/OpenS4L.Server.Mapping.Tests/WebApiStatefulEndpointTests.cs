using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Message.Game;
using OpenS4L.Plugins.WebApi;
using OpenS4L.Plugins.WebApi.Mappers;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using OpenS4L.Server.Game.Services;
using ProudNet;
using Serilog;
using Xunit;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Contract tests for the WebApi routes that need the Game PlayerManager/ChannelService/
    /// DatabaseService — hosted over ASP.NET TestServer backed by a GameTestContext.
    /// </summary>
    public class WebApiStatefulEndpointTests : IDisposable
    {
        private readonly GameTestContext _ctx = new GameTestContext();
        private readonly HttpClient _client;

        public WebApiStatefulEndpointTests()
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = null);

            var app = builder.Build();
            var gds = _ctx.GameData;
            Endpoints.Map(app, new CtxProvider(_ctx, gds), Log.Logger, new WebApiMapper(gds));
            app.StartAsync().GetAwaiter().GetResult();
            _client = app.GetTestClient();
        }

        public void Dispose()
        {
            _client.Dispose();
            _ctx.Dispose();
        }

        private async Task<Player> LoginAsync(uint accountId)
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
            return session.Player;
        }

        [Fact]
        public async Task Get_statistics_returnsPlayersOnline()
        {
            var plr = await LoginAsync(3101);
            var stats = await _client.GetFromJsonAsync<OpenS4L.Plugins.WebApi.Models.StatisticsDto>("/statistics");
            Assert.NotNull(stats);
            Assert.Equal(1, stats.PlayersOnline);
        }

        [Fact]
        public async Task Get_players_returnsLoggedIn()
        {
            var plr = await LoginAsync(3102);
            var players = await _client.GetFromJsonAsync<OpenS4L.Plugins.WebApi.Models.PlayerDto[]>("/players");
            Assert.NotNull(players);
            Assert.Contains(players, p => p.Id == 3102);
        }

        [Fact]
        public async Task Get_players_byId_found()
        {
            var plr = await LoginAsync(3103);
            var response = await _client.GetAsync("/players/3103");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Get_players_byId_missing_returns404()
        {
            var response = await _client.GetAsync("/players/999999");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Get_channels_returnsList()
        {
            // Seed a channel so ChannelService has entries.
            using (var db = _ctx.Get<GameContext>())
            {
                db.Channels.Add(new ChannelEntity { Id = 1, Name = "C", Description = "d", PlayerLimit = 32, Color = "FF0000", MinLevel = 0, MaxLevel = 99 });
                await db.SaveChangesAsync();
            }
            await _ctx.Get<ChannelService>().StartAsync(System.Threading.CancellationToken.None);

            var channels = await _client.GetFromJsonAsync<OpenS4L.Plugins.WebApi.Models.ChannelDto[]>("/channels");
            Assert.NotNull(channels);
            Assert.NotEmpty(channels);
        }

        [Fact]
        public async Task Get_channels_byId_found()
        {
            using (var db = _ctx.Get<GameContext>())
            {
                db.Channels.Add(new ChannelEntity { Id = 1, Name = "C", Description = "d", PlayerLimit = 32, Color = "FF0000", MinLevel = 0, MaxLevel = 99 });
                await db.SaveChangesAsync();
            }
            await _ctx.Get<ChannelService>().StartAsync(System.Threading.CancellationToken.None);

            var response = await _client.GetAsync("/channels/1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Post_admin_kick_form_disconnects()
        {
            var plr = await LoginAsync(3104);
            var response = await _client.PostAsync("/admin/kick",
                new FormUrlEncodedContent(new[] { new System.Collections.Generic.KeyValuePair<string, string>("playerId", "3104") }));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Post_admin_kick_missingPlayer_returns404()
        {
            var response = await _client.PostAsync("/admin/kick",
                new FormUrlEncodedContent(new[] { new System.Collections.Generic.KeyValuePair<string, string>("playerId", "999999") }));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Post_admin_ban_bansPlayer()
        {
            var plr = await LoginAsync(3105); // LoginAsync seeds the Auth account + player row.

            var response = await _client.PostAsJsonAsync("/admin/ban",
                new { playerId = 3105, duration = 86400, reason = "test ban" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using (var db = _ctx.Get<AuthContext>())
            {
                Assert.NotEmpty(await db.Bans.Where(x => x.AccountId == 3105).ToArrayAsync());
            }
        }

        [Fact]
        public async Task Post_admin_ban_missingPlayer_returns404()
        {
            var response = await _client.PostAsJsonAsync("/admin/ban",
                new { playerId = 999999, duration = 60, reason = "x" });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Post_admin_roomkick_notInRoom_returns404()
        {
            var plr = await LoginAsync(3106);
            var response = await _client.PostAsJsonAsync("/admin/roomkick", new { playerId = 3106 });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Post_admin_closeroom_missingRoom_returns404()
        {
            using (var db = _ctx.Get<GameContext>())
            {
                db.Channels.Add(new ChannelEntity { Id = 1, Name = "C", Description = "d", PlayerLimit = 32, Color = "FF0000", MinLevel = 0, MaxLevel = 99 });
                await db.SaveChangesAsync();
            }
            await _ctx.Get<ChannelService>().StartAsync(System.Threading.CancellationToken.None);

            var response = await _client.PostAsJsonAsync("/admin/closeroom", new { channelId = 1, roomId = 999 });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Get_rooms_byChannel_empty()
        {
            using (var db = _ctx.Get<GameContext>())
            {
                db.Channels.Add(new ChannelEntity { Id = 2, Name = "C2", Description = "d", PlayerLimit = 32, Color = "FF0000", MinLevel = 0, MaxLevel = 99 });
                await db.SaveChangesAsync();
            }
            await _ctx.Get<ChannelService>().StartAsync(System.Threading.CancellationToken.None);

            var rooms = await _client.GetFromJsonAsync<OpenS4L.Plugins.WebApi.Models.RoomDto[]>("/rooms/2");
            Assert.NotNull(rooms);
        }

        private sealed class CtxProvider : IServiceProvider
        {
            private readonly GameTestContext _ctx;
            private readonly GameDataService _gds;

            public CtxProvider(GameTestContext ctx, GameDataService gds)
            {
                _ctx = ctx;
                _gds = gds;
            }

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(GameDataService)) return _gds;
                if (serviceType == typeof(PlayerManager)) return _ctx.Get<PlayerManager>();
                if (serviceType == typeof(ChannelService)) return _ctx.Get<ChannelService>();
                if (serviceType == typeof(DatabaseService)) return _ctx.Get<DatabaseService>();
                return null;
            }
        }
    }
}
