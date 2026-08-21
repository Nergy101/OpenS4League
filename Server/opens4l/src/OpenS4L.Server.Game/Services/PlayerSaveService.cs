using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Queues;
using Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProudNet.Hosting.Services;

namespace OpenS4L.Server.Game.Services
{
    /// <summary>
    /// Producer for the player-save write-behind queue. On each publish tick it snapshots every
    /// dirty player into a self-contained <see cref="PlayerSaveSnapshot"/> and enqueues it, instead
    /// of writing Postgres directly. The durable Redis queue (or in-memory in tests) is what gives
    /// instance-crash safety: <see cref="PlayerSaveFlushService"/> drains it to Postgres, including
    /// on startup after a restart.
    /// </summary>
    public class PlayerSaveService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly AppOptions _appOptions;
        private readonly ISchedulerService _schedulerService;
        private readonly IQueue<PlayerSaveSnapshot> _queue;
        private readonly PlayerManager _playerManager;
        private readonly CancellationTokenSource _shutdown;

        public PlayerSaveService(ILogger<PlayerSaveService> logger, IOptions<AppOptions> appOptions,
            ISchedulerService schedulerService, IQueue<PlayerSaveSnapshot> queue, PlayerManager playerManager)
        {
            _logger = logger;
            _appOptions = appOptions.Value;
            _schedulerService = schedulerService;
            _queue = queue;
            _playerManager = playerManager;
            _shutdown = new CancellationTokenSource();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _schedulerService.ScheduleAsync(OnSave, null, null, _appOptions.SavePublishInterval, _shutdown.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _shutdown.Cancel();
            return Task.CompletedTask;
        }

        private async void OnSave(object _, object __)
        {
            if (_shutdown.IsCancellationRequested)
                return;

            await PublishDirtyPlayers();

            if (_shutdown.IsCancellationRequested)
                return;

            try
            {
                await _schedulerService.ScheduleAsync(OnSave, null, null, _appOptions.SavePublishInterval, _shutdown.Token);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to schedule next save");
            }
        }

        internal async Task PublishDirtyPlayers()
        {
            // Only players that actually changed since their last successful publish need
            // snapshotting. Each Player is the latest snapshot of its own state (absolute values),
            // so clean players are simply skipped.
            var dirtyPlayers = _playerManager
                .Where(plr => plr.Session.IsConnected && plr.HasPendingChanges)
                .ToList();

            if (dirtyPlayers.Count == 0)
                return; // Nothing changed — no queue writes.

            _logger.Information("Publishing {Count} dirty player(s) to save queue...", dirtyPlayers.Count);

            var published = new List<Player>();
            foreach (var plr in dirtyPlayers)
            {
                try
                {
                    var snapshot = plr.BuildSaveSnapshot();
                    await _queue.EnqueueAsync(snapshot);
                    published.Add(plr);
                }
                catch (Exception ex)
                {
                    // Leave the player dirty so the next publish tick retries — no data loss.
                    plr.AddContextToLogger(_logger).Error(ex, "Unable to enqueue save snapshot");
                }
            }

            // Only clear pending changes for players whose snapshot was durably enqueued.
            foreach (var plr in published)
                plr.ClearPendingChanges();
        }
    }
}
