using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Serializer;
using Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenS4L.Common;
using OpenS4L.Common.Configuration;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game;
using OpenS4L.Server.Game.GameRules;
using OpenS4L.Server.Game.Mappers;
using OpenS4L.Server.Game.Services;
using ProudNet;
using ProudNet.Hosting.Services;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Builds the Game server DI graph with in-memory fakes (bus, cache, EF) and a reflection-
    /// populated GameDataService, so real Game handlers/domain can be driven in-process.
    /// </summary>
    internal sealed class GameTestContext : IDisposable
    {
        public ServiceProvider Provider { get; }
        public InMemoryMessageBus Bus { get; }
        public GameDataService GameData { get; }
        private readonly InMemoryDatabaseRoot _dbRoot = new InMemoryDatabaseRoot();

        public GameTestContext(PostgresDatabase postgresDb = null)
        {
            Bus = new InMemoryMessageBus();
            GameData = GameFixtures.CreateGameDataService();
            // Seed items the login/character-creation flows create or grant.
            GameFixtures.SeedShopItem(GameData, (ItemNumber)6000015u); // slot coupon
            GameFixtures.SeedShopItem(GameData, (ItemNumber)2000006u); // dagger (default weapon)
            GameFixtures.SeedShopItem(GameData, (ItemNumber)3050001u); // shield (default skill)

            var services = new ServiceCollection();
            services.Configure<AppOptions>(x => x.ClientVersions = new[] { new Version(1, 0, 0, 0) });
            services.Configure<NetworkOptions>(x =>
            {
                x.Listener = new IPEndPoint(IPAddress.Loopback, 22000);
                x.MaxSessions = 1000;
            });
            services.Configure<ServerListOptions>(x => { x.Id = 1; x.Name = "game"; x.Address = "127.0.0.1"; });
            services.Configure<IdGeneratorOptions>(x => x.Id = 0);
            services.Configure<GameOptions>(x => x.MaxLevel = 99);
            services.Configure<ClanOptions>(x => { x.NameMinLength = 3; x.NameMaxLength = 16; x.DefaultIcon = "icon"; });
            services.Configure<DeathmatchOptions>(x => { });
            services.Configure<TouchdownOptions>(x => { });
            services.Configure<BattleRoyalOptions>(x => { });
            services.Configure<CaptainOptions>(x => { });

            // DB: InMemory EF by default (fast, isolated per test via the shared root). When a
            // Postgres database is supplied, use the real Npgsql provider so the EF
            // ExecuteUpdateAsync/ExecuteDeleteAsync paths are exercised.
            if (postgresDb == null)
            {
                services.AddDbContext<AuthContext>(x => x.UseInMemoryDatabase("game-auth", _dbRoot));
                services.AddDbContext<GameContext>(x => x.UseInMemoryDatabase("game-game", _dbRoot));
            }
            else
            {
                services.AddDbContext<AuthContext>(x => x.UseNpgsql(postgresDb.ConnectionString));
                services.AddDbContext<GameContext>(x => x.UseNpgsql(postgresDb.ConnectionString));
            }
            services.AddSingleton<DatabaseService>();

            services.AddTransient<ISerializer>(x => new Foundatio.Serializer.JsonNetSerializer());
            services.AddSingleton<ICacheClient>(new InMemoryCacheClient());
            services.AddSingleton<IMessageBus>(Bus);
            services.AddSingleton<IQueue<PlayerSaveSnapshot>>(new InMemoryQueue<PlayerSaveSnapshot>());

            services.AddSingleton(typeof(Logging.ILogger<>), typeof(Logging.Logger<>));
            services.AddSingleton(typeof(Logging.ILogger), typeof(Logging.Logger));
            services.AddSingleton<ILoggerFactory>(new LoggerFactory());

            services.AddSingleton<ISessionManager>(new FakeSessionManager());
            services.AddSingleton<ManualSchedulerService>();
            services.AddSingleton<ProudNet.Hosting.Services.ISchedulerService>(x => x.GetRequiredService<ManualSchedulerService>());
            services.AddSingleton<IdGeneratorService>();
            services.AddSingleton<GameMapper>();
            services.AddSingleton(GameData);
            services.AddSingleton<GameRuleResolver>();
            services.AddTransient<GameRuleStateMachine>();
            services.AddTransient<Deathmatch>();
            services.AddTransient<Touchdown>();
            services.AddTransient<BattleRoyal>();
            services.AddTransient<Captain>();
            services.AddTransient<Practice>();

            services.AddSingleton<PlayerManager>();
            services.AddSingleton<ChannelService>();
            services.AddTransient<RoomManager>();
            services.AddTransient<Room>();
            services.AddTransient<Player>();
            services.AddTransient<CharacterManager>();
            services.AddTransient<PlayerInventory>();
            services.AddSingleton<ClanManager>();
            services.AddTransient<Clan>();
            services.AddSingleton<NicknameLookupService>();
            services.AddSingleton<EquipValidator>();
            services.AddSingleton<PlayerSaveService>();
            services.AddSingleton<PlayerSaveFlushService>();
            services.AddSingleton<IpcService>();
            services.AddSingleton<ServerlistService>();
            // Stub IApplicationLifetime (AdminCommands.Shutdown needs it).
            services.AddSingleton<Microsoft.Extensions.Hosting.IApplicationLifetime>(new FakeApplicationLifetime());
            services.AddCommands(typeof(Program).Assembly);
            services.AddSingleton<CommandService>();

            // Game message handlers.
            var resolver = new DefaultMessageHandlerResolver(
                new[] { typeof(Program).Assembly }, typeof(IGameMessage));
            foreach (var handlerType in resolver.GetImplementations())
                services.AddTransient(handlerType);

            Provider = services.BuildServiceProvider();
        }

        public (Game.Session session, FakeSocketChannel channel) CreateSession(uint hostId = 1)
        {
            var channel = new FakeSocketChannel(new IPEndPoint(IPAddress.Loopback, 22000 + (int)hostId));
            var session = new Game.Session(new Logger<Game.Session>(), hostId, channel);
            return (session, channel);
        }

        public T Get<T>() where T : class => Provider.GetRequiredService<T>();
        public void Dispose() => Provider.Dispose();
    }
}
