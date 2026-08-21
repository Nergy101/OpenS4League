using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenS4L.Database;
using Testcontainers.PostgreSql;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Shared real-Postgres server for tests (Testcontainers). A single container is started once
    /// and reused; a <c>s4l_template</c> database is migrated once, then each test context clones it
    /// via <c>CREATE DATABASE ... TEMPLATE</c> for fast, fully-isolated per-test databases. This
    /// exercises the real Npgsql provider, which supports the EF ExecuteUpdateAsync/ExecuteDeleteAsync
    /// paths that the InMemory provider cannot.
    /// </summary>
    internal sealed class PostgresFixture : IAsyncDisposable
    {
        private static readonly Lazy<PostgresFixture> s_instance =
            new Lazy<PostgresFixture>(() => new PostgresFixture());

        public static PostgresFixture Instance => s_instance.Value;

        private readonly PostgreSqlContainer _container;
        private bool _templateReady;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        private PostgresFixture()
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("postgres")
                .WithUsername("test")
                .WithPassword("test")
                .Build();
        }

        /// <summary>Starts the container and migrates the template database (idempotent).</summary>
        public async Task EnsureReadyAsync()
        {
            if (_templateReady)
                return;

            await _gate.WaitAsync();
            try
            {
                if (_templateReady)
                    return;

                await _container.StartAsync();

                // Build + migrate the template once.
                var connStr = _container.GetConnectionString();
                await using (var conn = new NpgsqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    await using (var drop = new NpgsqlCommand("DROP DATABASE IF EXISTS s4l_template", conn))
                        await drop.ExecuteNonQueryAsync();
                    await using (var create = new NpgsqlCommand("CREATE DATABASE s4l_template", conn))
                        await create.ExecuteNonQueryAsync();
                }

                var templateConn = new NpgsqlConnectionStringBuilder(connStr) { Database = "s4l_template" }.ConnectionString;
                await using (var auth = CreateContext<AuthContext>(templateConn))
                    await auth.Database.MigrateAsync();
                await using (var game = CreateContext<GameContext>(templateConn))
                    await game.Database.MigrateAsync();

                // Drop pooled connections to the template so it can be cloned.
                await TerminateConnectionsAsync("s4l_template");

                _templateReady = true;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Clones the migrated template into a fresh, isolated database.</summary>
        public async Task<PostgresDatabase> CreateDatabaseAsync()
        {
            await EnsureReadyAsync();

            await _gate.WaitAsync();
            try
            {
                var dbName = "s4l_test_" + Guid.NewGuid().ToString("N");
                await using (var conn = new NpgsqlConnection(_container.GetConnectionString()))
                {
                    await conn.OpenAsync();
                    await TerminateConnectionsAsync("s4l_template", conn);
                    await using (var cmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\" TEMPLATE s4l_template", conn))
                        await cmd.ExecuteNonQueryAsync();
                }

                var connStr = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
                {
                    Database = dbName
                }.ConnectionString;

                return new PostgresDatabase(dbName, connStr);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task TerminateConnectionsAsync(string dbName, NpgsqlConnection conn = null)
        {
            var owns = conn == null;
            if (owns)
            {
                conn = new NpgsqlConnection(_container.GetConnectionString());
                await conn.OpenAsync();
            }
            await using (var kill = new NpgsqlCommand(
                $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{dbName}' AND pid <> pg_backend_pid()", conn))
                await kill.ExecuteNonQueryAsync();
            if (owns) await conn.DisposeAsync();
        }

        public async Task DropDatabaseAsync(PostgresDatabase db)
        {
            await using (var conn = new NpgsqlConnection(_container.GetConnectionString()))
            {
                await conn.OpenAsync();
                await using (var kill = new NpgsqlCommand(
                    $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{db.Name}' AND pid <> pg_backend_pid()", conn))
                    await kill.ExecuteNonQueryAsync();
                await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{db.Name}\"", conn))
                    await drop.ExecuteNonQueryAsync();
            }
        }

        private static TContext CreateContext<TContext>(string connStr) where TContext : DbContext
        {
            var options = new DbContextOptionsBuilder<TContext>()
                .UseNpgsql(connStr)
                .Options;
            return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        }

        public async ValueTask DisposeAsync()
        {
            await _container.StopAsync();
        }
    }

    /// <summary>Handle to an isolated test database.</summary>
    internal sealed class PostgresDatabase : IAsyncDisposable
    {
        public string Name { get; }
        public string ConnectionString { get; }

        public PostgresDatabase(string name, string connectionString)
        {
            Name = name;
            ConnectionString = connectionString;
        }

        public async ValueTask DisposeAsync() => await PostgresFixture.Instance.DropDatabaseAsync(this);
    }
}
