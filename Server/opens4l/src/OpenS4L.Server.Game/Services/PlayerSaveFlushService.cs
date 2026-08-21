using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Queues;
using Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenS4L.Database;
using ProudNet.Hosting.Services;

namespace OpenS4L.Server.Game.Services
{
    /// <summary>
    /// Consumer for the player-save write-behind queue. Drains <see cref="PlayerSaveSnapshot"/>s,
    /// coalesces by <see cref="PlayerSaveSnapshot.AccountId"/> (latest wins — safe because
    /// snapshots carry absolute values), and bulk-flushes them to Postgres via
    /// <see cref="PlayerSaveWriter"/>. On startup it drains anything a previous game instance left
    /// behind, so a killed instance loses at most the last publish interval.
    /// </summary>
    public class PlayerSaveFlushService : IHostedService
    {
        private const int MaxBatch = 500;

        private readonly ILogger _logger;
        private readonly AppOptions _appOptions;
        private readonly IQueue<PlayerSaveSnapshot> _queue;
        private readonly DatabaseService _databaseService;
        private readonly ISchedulerService _schedulerService;
        private readonly CancellationTokenSource _shutdown;

        public PlayerSaveFlushService(ILogger<PlayerSaveFlushService> logger, IOptions<AppOptions> appOptions,
            IQueue<PlayerSaveSnapshot> queue, DatabaseService databaseService, ISchedulerService schedulerService)
        {
            _logger = logger;
            _appOptions = appOptions.Value;
            _queue = queue;
            _databaseService = databaseService;
            _schedulerService = schedulerService;
            _shutdown = new CancellationTokenSource();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Recovery drain first: flush anything a previous instance left in the queue before
            // we start accepting new publishes.
            return FlushAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _shutdown.Cancel();
            return Task.CompletedTask;
        }

        internal async Task FlushAsync()
        {
            if (_shutdown.IsCancellationRequested)
                return;

            var entries = new List<IQueueEntry<PlayerSaveSnapshot>>();
            try
            {
                // Lease up to a batch of work items (non-blocking; null when the queue is empty).
                for (var i = 0; i < MaxBatch; ++i)
                {
                    var entry = await _queue.DequeueAsync(TimeSpan.Zero);
                    if (entry == null)
                        break;
                    entries.Add(entry);
                }

                if (entries.Count == 0)
                    return;

                // Coalesce by account — the latest snapshot for each player wins.
                var latestByAccount = new Dictionary<int, PlayerSaveSnapshot>();
                foreach (var entry in entries)
                    latestByAccount[entry.Value.AccountId] = entry.Value;

                _logger.Information("Flushing {Count} save snapshot(s) for {Accounts} player(s)...",
                    entries.Count, latestByAccount.Count);

                using (var db = _databaseService.Open<GameContext>())
                {
                    foreach (var s in latestByAccount.Values)
                    {
                        PlayerSaveWriter.WritePlayer(db, s);
                        await PlayerSaveWriter.WriteInventory(db, s);
                        await PlayerSaveWriter.WriteCharacters(db, s);
                    }

                    // One batched write for the whole flush.
                    await db.SaveChangesAsync();
                }

                // Acknowledge (remove) only the entries we successfully persisted.
                foreach (var entry in entries)
                    await _queue.CompleteAsync(entry);
            }
            catch (Exception ex)
            {
                // Leased entries are left uncompleted so the queue redelivers them on retry —
                // nothing is silently dropped.
                _logger.Error(ex, "Unable to flush player saves; leaving items queued for retry");
            }

            if (_shutdown.IsCancellationRequested)
                return;

            try
            {
                await _schedulerService.ScheduleAsync((_, __) => { _ = FlushAsync(); }, null, null,
                    _appOptions.SaveFlushInterval, _shutdown.Token);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to schedule next flush");
            }
        }
    }
}
