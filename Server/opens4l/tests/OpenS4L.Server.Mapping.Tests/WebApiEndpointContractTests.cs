using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenS4L.Plugins.WebApi;
using OpenS4L.Plugins.WebApi.Mappers;
using OpenS4L.Server.Game.Services;
using Serilog;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Contract tests for the WebApi HTTP routes that can be exercised without a live network
    /// transport. We host the minimal API in-process over ASP.NET TestServer with a fake
    /// IServiceProvider that supplies GameDataService (which the /gamedata routes need). The
    /// routes needing PlayerManager/ChannelService/Room are covered by the transport-fake harness.
    /// </summary>
    public class WebApiEndpointContractTests : IDisposable
    {
        private readonly HttpClient _client;
        private readonly GameDataService _gds = GameFixtures.CreateGameDataService();

        public WebApiEndpointContractTests()
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = null);

            var app = builder.Build();
            Endpoints.Map(app, new FakeProvider(_gds), Log.Logger, new WebApiMapper(_gds));
            app.StartAsync().GetAwaiter().GetResult();
            _client = app.GetTestClient();
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        [Fact]
        public async Task Get_gamedata_maps_returnsList()
        {
            var maps = await _client.GetFromJsonAsync<OpenS4L.Plugins.WebApi.Models.MapDto[]>("/gamedata/maps");
            Assert.NotNull(maps);
            Assert.NotEmpty(maps);
        }

        [Fact]
        public async Task Get_gamedata_maps_byId_found()
        {
            var response = await _client.GetAsync("/gamedata/maps/3");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var map = await response.Content.ReadFromJsonAsync<OpenS4L.Plugins.WebApi.Models.MapDto>();
            Assert.NotNull(map);
            Assert.Equal(3, map.Id);
        }

        [Fact]
        public async Task Get_gamedata_maps_byId_missing_returns404()
        {
            var response = await _client.GetAsync("/gamedata/maps/99999");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Get_gamedata_items_byId_found()
        {
            var response = await _client.GetAsync("/gamedata/items/2010001");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var item = await response.Content.ReadFromJsonAsync<OpenS4L.Plugins.WebApi.Models.ItemDto>();
            Assert.NotNull(item);
            Assert.Equal(2010001u, item.Id);
        }

        [Fact]
        public async Task Get_gamedata_items_byId_missing_throwsKeyNotFound()
        {
            // NOTE: the /gamedata/items/{id} handler uses `Items[(uint)itemId]` (dictionary indexer),
            // which THROWS KeyNotFoundException for a missing key instead of returning 404. The
            // `== null` check after it is dead code. This is a documented latent bug — pinned here
            // as the ACTUAL behavior. (Under real Kestrel it surfaces as a 500; under TestServer
            // the unhandled exception bubbles out of GetAsync.) Fixing it should flip this test
            // to assert a 404.
            var ex = await Assert.ThrowsAsync<System.Collections.Generic.KeyNotFoundException>(
                () => _client.GetAsync("/gamedata/items/99999"));
            Assert.Contains("99999", ex.Message);
        }

        [Fact]
        public async Task Get_unknown_route_returns404()
        {
            var response = await _client.GetAsync("/nonexistent");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        private sealed class FakeProvider : IServiceProvider
        {
            private readonly GameDataService _gds;

            public FakeProvider(GameDataService gds) => _gds = gds;

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(GameDataService))
                    return _gds;
                return null;
            }
        }
    }
}
