using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace OpenS4L.Server.Chat
{
    /// <summary>
    /// A tiny HTTP /statistics endpoint for the chat server, so the admin console can show
    /// "messages sent" and other live chat metrics. Hosted with Kestrel (Microsoft.AspNetCore.App,
    /// provided by the aspnet:10.0 base image) — the same approach the game server's WebApi plugin
    /// uses. Serves /statistics as PascalCase JSON: { Uptime, MessagesSent, WhispersSent }.
    /// </summary>
    public class ChatMetricsService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly MetricsOptions _options;
        private WebApplication _app;

        public ChatMetricsService(ILogger<ChatMetricsService> logger, IOptions<MetricsOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                ApplicationName = "OpenS4L.Server.Chat.Metrics"
            });
            builder.WebHost.UseUrls(_options.Listener);
            // PascalCase to match the game WebApi /statistics wire format.
            builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = null);

            _app = builder.Build();

            _app.MapGet("/statistics", () =>
            {
                var uptime = (long)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
                return Results.Json(new
                {
                    Uptime = uptime,
                    MessagesSent = ChatMetrics.MessagesSent,
                    WhispersSent = ChatMetrics.WhispersSent
                });
            });

            await _app.StartAsync(cancellationToken);
            _logger.Information("Chat metrics listening on {Listener}", _options.Listener);
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
