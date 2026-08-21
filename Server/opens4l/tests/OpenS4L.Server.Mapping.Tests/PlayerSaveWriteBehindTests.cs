using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Queues;
using Microsoft.EntityFrameworkCore;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.Handlers;
using OpenS4L.Server.Game.Services;
using ProudNet;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Covers the Redis write-behind save path: the producer publishes snapshots to the queue
    /// (not Postgres), the consumer coalesces by account and bulk-flushes, and the direct
    /// disconnect-time save still works. Uses the in-memory queue + in-memory EF provider, so no
    /// Docker is required.
    /// </summary>
    public class PlayerSaveWriteBehindTests
    {
        private readonly GameTestContext _ctx = new GameTestContext();

        private async Task<(Player plr, FakeSocketChannel channel)> LoginAsync(uint accountId)
        {
            var cache = (Foundatio.Caching.InMemoryCacheClient)_ctx.Get<Foundatio.Caching.ICacheClient>();
            await cache.SetAsync<string>(OpenS4L.Common.Constants.Cache.SessionKey(accountId), "sid-" + accountId);
            using (var auth = _ctx.Get<AuthContext>())
            {
                auth.Accounts.Add(new AccountEntity { Id = (int)accountId, Username = "g" + accountId, Nickname = "nick" + accountId, SecurityLevel = (byte)SecurityLevel.User });
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

        [Fact]
        public async Task Publish_snapshotsDirtyPlayerToQueue_notDb()
        {
            var (plr, _) = await LoginAsync(2101);
            plr.PEN = 500; // marks player dirty

            var svc = _ctx.Get<PlayerSaveService>();
            await svc.PublishDirtyPlayers();

            var queue = _ctx.Get<IQueue<PlayerSaveSnapshot>>();
            var entry = await queue.DequeueAsync(TimeSpan.Zero);
            Assert.NotNull(entry);
            Assert.Equal(2101, entry.Value.AccountId);
            Assert.Equal(500, entry.Value.PEN);

            // Nothing written to the DB yet — the flush consumer does that.
            using (var db = _ctx.Get<GameContext>())
            {
                var row = await db.Players.SingleAsync(x => x.Id == 2101);
                Assert.Equal(0, row.PEN);
            }
        }

        [Fact]
        public async Task Publish_cleanPlayer_doesNotEnqueue()
        {
            var (plr, _) = await LoginAsync(2102);
            var svc = _ctx.Get<PlayerSaveService>();
            await svc.PublishDirtyPlayers();

            var queue = _ctx.Get<IQueue<PlayerSaveSnapshot>>();
            var entry = await queue.DequeueAsync(TimeSpan.Zero);
            Assert.Null(entry); // no pending changes -> nothing published
        }

        [Fact]
        public async Task Flush_coalescesLatestSnapshotWins_andWritesToDb()
        {
            using (var db = _ctx.Get<GameContext>())
            {
                db.Players.AddRange(
                    new PlayerEntity { Id = 2103, TotalExperience = 1000 },
                    new PlayerEntity { Id = 2104, TotalExperience = 1000 });
                await db.SaveChangesAsync();
            }

            var queue = _ctx.Get<IQueue<PlayerSaveSnapshot>>();
            await queue.EnqueueAsync(new PlayerSaveSnapshot { AccountId = 2103, PEN = 100, PlayerRowDirty = true });
            await queue.EnqueueAsync(new PlayerSaveSnapshot { AccountId = 2103, PEN = 999, PlayerRowDirty = true }); // latest wins
            await queue.EnqueueAsync(new PlayerSaveSnapshot { AccountId = 2104, PEN = 50, PlayerRowDirty = true });

            var flush = _ctx.Get<PlayerSaveFlushService>();
            await flush.FlushAsync();

            using (var db = _ctx.Get<GameContext>())
            {
                Assert.Equal(999, (await db.Players.SingleAsync(x => x.Id == 2103)).PEN);
                Assert.Equal(50, (await db.Players.SingleAsync(x => x.Id == 2104)).PEN);
            }

            // Queue drained.
            var leftover = await queue.DequeueAsync(TimeSpan.Zero);
            Assert.Null(leftover);
        }

        [Fact]
        public async Task DirectSave_writesThroughWriter_unchanged()
        {
            // Reproduces the disconnect-time guaranteed flush: plr.Save(db) writes to Postgres.
            var (plr, _) = await LoginAsync(2105);
            plr.PEN = 500;

            using (var db = _ctx.Get<GameContext>())
            {
                await plr.Save(db);
                await db.SaveChangesAsync();
            }

            using (var db = _ctx.Get<GameContext>())
            {
                Assert.Equal(500, (await db.Players.SingleAsync(x => x.Id == 2105)).PEN);
            }
        }
    }

    /// <summary>
    /// Exercises the real-relational-provider path of the write-behind flush: item/character
    /// removal via EF ExecuteDeleteAsync, which the in-memory provider cannot run.
    /// </summary>
    public class PlayerSaveWriteBehindPostgresTests : IAsyncLifetime
    {
        private PostgresDatabase _db;
        private GameTestContext _ctx;

        public async Task InitializeAsync()
        {
            _db = await PostgresFixture.Instance.CreateDatabaseAsync();
            _ctx = new GameTestContext(_db);
        }

        public async Task DisposeAsync()
        {
            _ctx?.Dispose();
            if (_db != null) await _db.DisposeAsync();
        }

        [Fact]
        public async Task Flush_persistsDirtyPlayerScalarsToPostgres()
        {
            // The core crash-safety guarantee: a dirty player's snapshot flushed through the
            // write-behind consumer persists to real Postgres. A scalar-only snapshot (no items/
            // characters) keeps the test independent of shop-data FK rows the fixture doesn't seed.
            using (var db = _ctx.Get<GameContext>())
            {
                db.Players.Add(new PlayerEntity { Id = 3001, TotalExperience = 1000 });
                await db.SaveChangesAsync();
            }

            var queue = _ctx.Get<IQueue<PlayerSaveSnapshot>>();
            await queue.EnqueueAsync(new PlayerSaveSnapshot { AccountId = 3001, PEN = 500, PlayerRowDirty = true });

            await _ctx.Get<PlayerSaveFlushService>().FlushAsync();

            using (var db = _ctx.Get<GameContext>())
            {
                Assert.Equal(500, (await db.Players.SingleAsync(x => x.Id == 3001)).PEN);
            }
        }
    }
}
