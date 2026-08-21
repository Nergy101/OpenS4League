using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenS4L.Common.Configuration;
using OpenS4L.Plugins.WebApi;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Exercises the WebApiService host lifecycle (StartAsync binds a real Kestrel port; StopAsync
    /// shuts it down) over a real GameTestContext provider.
    /// </summary>
    public class WebApiServiceTests : IAsyncLifetime
    {
        private GameTestContext _ctx;
        private WebApiService _svc;

        public async Task InitializeAsync()
        {
            _ctx = new GameTestContext();
        }

        public async Task DisposeAsync()
        {
            if (_svc != null) await _svc.StopAsync(CancellationToken.None);
            _ctx?.Dispose();
        }

        [Fact]
        public async Task StartStop_bindsAndReleases()
        {
            _svc = new WebApiService(
                _ctx.Provider,
                Options.Create(new WebApiOptions { Listener = "http://127.0.0.1:0" }), // ephemeral port
                _ctx.GameData);

            await _svc.StartAsync(CancellationToken.None);
            // The service started and is listening.
            await _svc.StopAsync(CancellationToken.None);
            _svc = null;
        }
    }
}
