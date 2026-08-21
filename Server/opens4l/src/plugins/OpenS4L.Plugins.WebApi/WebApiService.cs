using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenS4L.Common;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Data;
using OpenS4L.Server.Game.Services;
using Serilog;
using OpenS4L.Plugins.WebApi.Models;
using Constants = Serilog.Core.Constants;

namespace OpenS4L.Plugins.WebApi
{
    public class WebApiService : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly WebApiOptions _options;
        private readonly GameDataService _gameDataService;
        private readonly ILogger _logger;
        private WebApplication _app;

        public WebApiService(IServiceProvider serviceProvider, IOptions<WebApiOptions> options, GameDataService gameDataService)
        {
            _services = serviceProvider;
            _options = options.Value;
            _gameDataService = gameDataService;
            _logger = Log.ForContext(Constants.SourceContextPropertyName, "WebApiService");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                ApplicationName = "OpenS4L.Plugins.WebApi"
            });
            builder.WebHost.UseUrls(_options.Listener);

            // Match Swan's legacy wire format (PascalCase) instead of ASP.NET Core's default camelCase,
            // so existing consumers of the old JSON contract don't break.
            builder.Services.ConfigureHttpJsonOptions(o =>
                o.SerializerOptions.PropertyNamingPolicy = null);

            _app = builder.Build();
            Endpoints.Map(_app, _services, _logger, new Mappers.WebApiMapper(_gameDataService));

            await _app.StartAsync(cancellationToken);
            _logger.Information("Web API listening on {Listener}", _options.Listener);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_app != null)
            {
                await _app.StopAsync(cancellationToken);
                await _app.DisposeAsync();
                _app = null;
            }
        }
    }
}
