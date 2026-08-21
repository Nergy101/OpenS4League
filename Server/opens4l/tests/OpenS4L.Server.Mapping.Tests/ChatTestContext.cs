using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Serializer;
using Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenS4L.Common;
using OpenS4L.Common.Messaging;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Message.Chat;
using OpenS4L.Server.Chat;
using OpenS4L.Server.Chat.Mappers;
using OpenS4L.Server.Chat.Services;
using ProudNet;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Builds the full Chat server DI graph with in-memory fakes (cache, bus, EF) instead of
    /// Redis/Postgres, so real handlers and managers can be driven in-process. Mirrors the
    /// registrations in OpenS4L.Server.Chat.Program with the network/Redis pieces swapped out.
    /// </summary>
    internal sealed class ChatTestContext : IDisposable
    {
        public ServiceProvider Provider { get; }
        public InMemoryMessageBus Bus { get; }
        public InMemoryCacheClient Cache { get; }
        private readonly InMemoryDatabaseRoot _dbRoot = new InMemoryDatabaseRoot();

        public ChatTestContext(PostgresDatabase postgresDb = null)
        {
            Bus = new InMemoryMessageBus();
            Cache = new InMemoryCacheClient();

            var services = new ServiceCollection();

            // Options
            services.Configure<AppOptions>(x => { });
            services.Configure<OpenS4L.Common.Configuration.NetworkOptions>(x =>
            {
                x.Listener = new IPEndPoint(IPAddress.Loopback, 21000);
                x.MaxSessions = 1000;
            });
            services.Configure<OpenS4L.Common.Configuration.ServerListOptions>(x =>
            {
                x.Id = 1;
                x.Name = "test";
                x.Address = "127.0.0.1";
                x.UpdateInterval = TimeSpan.FromHours(1);
            });
            services.Configure<OpenS4L.Common.Configuration.IdGeneratorOptions>(x => x.Id = 1);

            // Scheduler (ServerlistService uses it).
            services.AddSingleton<ProudNet.Hosting.Services.ISchedulerService>(new NoopSchedulerService());

            // DB: InMemory EF by default (fast, isolated via the shared root). When a Postgres
            // database is supplied, use the real Npgsql provider (supports ExecuteUpdate/DeleteAsync).
            if (postgresDb == null)
            {
                services.AddDbContext<AuthContext>(x => x.UseInMemoryDatabase("auth-test", _dbRoot));
                services.AddDbContext<GameContext>(x => x.UseInMemoryDatabase("game-test", _dbRoot));
            }
            else
            {
                services.AddDbContext<AuthContext>(x => x.UseNpgsql(postgresDb.ConnectionString));
                services.AddDbContext<GameContext>(x => x.UseNpgsql(postgresDb.ConnectionString));
            }
            services.AddSingleton<DatabaseService>();

            // Cache/bus/serializer: in-memory
            services.AddTransient<ISerializer>(x => new JsonNetSerializer());
            services.AddSingleton<ICacheClient>(Cache);
            services.AddSingleton<IMessageBus>(Bus);

            // Custom Logging.ILogger (wraps Serilog). Register the open generic.
            services.AddSingleton(typeof(Logging.ILogger<>), typeof(Logging.Logger<>));
            services.AddSingleton(typeof(Logging.ILogger), typeof(Logging.Logger));

            // ProudNet session manager: fake that lets tests raise connect/disconnect.
            services.AddSingleton<ISessionManager>(new FakeSessionManager());
            // Scheduler: manual so tests can drive ServerlistService.Update.
            services.AddSingleton<ManualSchedulerService>();
            services.AddSingleton<ProudNet.Hosting.Services.ISchedulerService>(x => x.GetRequiredService<ManualSchedulerService>());

            services.AddSingleton<IdGeneratorService>();
            services.AddSingleton<ChatMapper>(x =>
                new ChatMapper(x.GetRequiredService<IOptions<OpenS4L.Common.Configuration.ServerListOptions>>().Value.Id));

            // Chat domain
            services.AddTransient<Player>();
            services.AddTransient<Mailbox>();
            services.AddTransient<DenyManager>();
            services.AddTransient<FriendManager>();
            services.AddTransient<PlayerSettingManager>();
            services.AddSingleton<PlayerManager>();
            services.AddSingleton<ChannelManager>();
            services.AddTransient<IpcService>();
            services.AddTransient<ServerlistService>();

            // Register all chat message handlers (ProudNet's UseProudNetServer auto-registers these
            // via DefaultMessageHandlerResolver; we do the same so tests can resolve them).
            var resolver = new DefaultMessageHandlerResolver(
                new[] { typeof(ChatTestContext).Assembly, typeof(Program).Assembly }, typeof(IChatMessage));
            foreach (var handlerType in resolver.GetImplementations())
                services.AddTransient(handlerType);

            Provider = services.BuildServiceProvider();

            // Register the level-from-experience responder on the bus (the Game server normally hosts it).
            Provider.GetRequiredService<IMessageBus>()
                .SubscribeToRequestAsync<LevelFromExperienceRequest, LevelFromExperienceResponse>(
                    req => Task.FromResult(new LevelFromExperienceResponse(1)), CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        /// <summary>Builds a real Chat Session over a fresh fake channel.</summary>
        public (Session session, FakeSocketChannel channel) CreateSession(uint hostId = 1)
        {
            var channel = new FakeSocketChannel(new IPEndPoint(IPAddress.Loopback, 21000 + (int)hostId));
            var session = new Session(new Logger<Session>(), hostId, channel);
            return (session, channel);
        }

        public T Get<T>() where T : class => Provider.GetRequiredService<T>();

        public void Dispose() => Provider.Dispose();
    }
}
