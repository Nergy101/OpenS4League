using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNetty.Transport.Channels;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Serializer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenS4L.Common;
using OpenS4L.Common.Configuration;
using OpenS4L.Common.Plugins;
using OpenS4L.Database;
using OpenS4L.Network.Data.Club;
using OpenS4L.Network.Data.Game;
using OpenS4L.Network.Data.GameRule;
using OpenS4L.Network.Message.Club;
using OpenS4L.Network.Message.Game;
using OpenS4L.Network.Message.GameRule;
using OpenS4L.Network.Serializers;
using OpenS4L.Server.Game.GameRules;
using OpenS4L.Server.Game.Serializers;
using OpenS4L.Server.Game.Services;
using Newtonsoft.Json;
using ProudNet;
using ProudNet.Hosting;
using ProudNet.Hosting.Services;
using Serilog;
using StackExchange.Redis;

namespace OpenS4L.Server.Game
{
    internal static class Program
    {
        public static string BaseDirectory { get; private set; }

        private static void Main()
        {
            BaseDirectory = Environment.GetEnvironmentVariable("OPENS4L_BASEDIR_GAME");
            if (string.IsNullOrWhiteSpace(BaseDirectory))
                BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            var configuration = Startup.Initialize(BaseDirectory, "config.hjson",
                x => x.GetSection(nameof(AppOptions.Logging)).Get<LoggerOptions>());

            Log.Information("Starting...");

            var appOptions = configuration.Get<AppOptions>();
            var hostBuilder = new HostBuilder();
            var redisConnectionMultiplexer = ConnectionMultiplexer.Connect(appOptions.Database.ConnectionStrings.Redis);

            IPluginHost pluginHost = new ScanPluginHost();
            pluginHost.Initialize(configuration, Path.Combine(BaseDirectory, "plugins"));

            hostBuilder
                .ConfigureHostConfiguration(builder => builder.AddConfiguration(configuration))
                .ConfigureAppConfiguration(builder => builder.AddConfiguration(configuration))
                .UseConsoleLifetime()
                .UseProudNetServer(builder =>
                {
                    var messageHandlerResolver = new DefaultMessageHandlerResolver(
                        AppDomain.CurrentDomain.GetAssemblies(),
                        typeof(IGameMessage),
                        typeof(IGameRuleMessage),
                        typeof(IClubMessage)
                    );

                    builder
                        .UseHostIdFactory<HostIdFactory>()
                        .UseSessionFactory<SessionFactory>()
                        .AddMessageFactory<GameMessageFactory>()
                        .AddMessageFactory<GameRuleMessageFactory>()
                        .AddMessageFactory<ClubMessageFactory>()
                        .UseMessageHandlerResolver(messageHandlerResolver)
                        .UseNetworkConfiguration((context, options) =>
                        {
                            options.Version = new Guid("{beb92241-8333-4117-ab92-9b4af78c688f}");
                            options.TcpListener = appOptions.Network.Listener;
                        })
                        .UseThreadingConfiguration((context, options) =>
                        {
                            options.SocketListenerThreadsFactory = () => new MultithreadEventLoopGroup(1);
                            options.SocketWorkerThreadsFactory = () => appOptions.Network.WorkerThreads < 1
                                ? new MultithreadEventLoopGroup()
                                : new MultithreadEventLoopGroup(appOptions.Network.WorkerThreads);
                            options.WorkerThreadFactory = () => new SingleThreadEventLoop();
                        })
                        .ConfigureSerializer(serializer =>
                        {
                            serializer.AddSerializer(new CharacterStyleSerializer());
                            serializer.AddSerializer(new ItemNumberSerializer());
                            serializer.AddSerializer(new VersionSerializer());
                            serializer.AddSerializer(new ShopPriceSerializer());
                            serializer.AddSerializer(new ShopEffectSerializer());
                            serializer.AddSerializer(new ShopItemSerializer());
                            serializer.AddSerializer(new PeerIdSerializer());
                            serializer.AddSerializer(new LongPeerIdSerializer());
                        });
                })
                .ConfigureServices((context, services) =>
                {
                    services
                        .Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true)
                        .Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromMinutes(1))
                        .Configure<AppOptions>(context.Configuration)
                        .Configure<NetworkOptions>(context.Configuration.GetSection(nameof(AppOptions.Network)))
                        .Configure<ServerListOptions>(context.Configuration.GetSection(nameof(AppOptions.ServerList)))
                        .Configure<DatabaseOptions>(context.Configuration.GetSection(nameof(AppOptions.Database)))
                        .Configure<GameOptions>(context.Configuration.GetSection(nameof(AppOptions.Game)))
                        .Configure<ClanOptions>(context.Configuration
                            .GetSection(nameof(AppOptions.Game))
                            .GetSection(nameof(AppOptions.Game.ClanOptions)))
                        .Configure<DeathmatchOptions>(context.Configuration
                            .GetSection(nameof(AppOptions.Game))
                            .GetSection(nameof(AppOptions.Game.Deathmatch)))
                        .Configure<TouchdownOptions>(context.Configuration
                            .GetSection(nameof(AppOptions.Game))
                            .GetSection(nameof(AppOptions.Game.Touchdown)))
                        .Configure<BattleRoyalOptions>(context.Configuration
                            .GetSection(nameof(AppOptions.Game))
                            .GetSection(nameof(AppOptions.Game.BattleRoyal)))
                        .Configure<CaptainOptions>(context.Configuration
                            .GetSection(nameof(AppOptions.Game))
                            .GetSection(nameof(AppOptions.Game.Captain)))
                        .Configure<IdGeneratorOptions>(x => x.Id = 0)
                        .AddSingleton<DatabaseService>()
                        .AddDbContext<AuthContext>(x => x.UseNpgsql(appOptions.Database.ConnectionStrings.Auth))
                        .AddDbContext<GameContext>(x => x.UseNpgsql(appOptions.Database.ConnectionStrings.Game))
                        .AddSingleton(redisConnectionMultiplexer)
                        .AddTransient<ISerializer>(x => new JsonNetSerializer(JsonConvert.DefaultSettings()))
                        .AddSingleton<ICacheClient, RedisCacheClient>()
                        .AddSingleton<IMessageBus, RedisMessageBus>()
                        .AddSingleton(x => new RedisCacheClientOptions
                        {
                            ConnectionMultiplexer = x.GetRequiredService<ConnectionMultiplexer>(),
                            Serializer = x.GetRequiredService<ISerializer>()
                        })
                        .AddSingleton(x => new RedisMessageBusOptions
                        {
                            Subscriber = x.GetRequiredService<ConnectionMultiplexer>().GetSubscriber(),
                            Serializer = x.GetRequiredService<ISerializer>()
                        })
                        .AddSingleton(x => new RedisQueueOptions<PlayerSaveSnapshot>
                        {
                            ConnectionMultiplexer = x.GetRequiredService<ConnectionMultiplexer>(),
                            Name = "opens4l:player-saves",
                            Retries = 3,
                            RetryDelay = TimeSpan.FromSeconds(2)
                        })
                        .AddSingleton<IQueue<PlayerSaveSnapshot>, RedisQueue<PlayerSaveSnapshot>>()
                        .AddTransient<Player>()
                        .AddTransient<CharacterManager>()
                        .AddTransient<PlayerInventory>()
                        .AddSingleton<PlayerManager>()
                        .AddTransient<RoomManager>()
                        .AddTransient<Room>()
                        .AddSingleton<GameRuleResolver>()
                        .AddTransient<GameRuleStateMachine>()
                        .AddTransient<Deathmatch>()
                        .AddTransient<Touchdown>()
                        .AddTransient<BattleRoyal>()
                        .AddTransient<Captain>()
                        .AddTransient<Practice>()
                        .AddTransient<Captain>()
                        .AddSingleton<EquipValidator>()
                        .AddTransient<Clan>()
                        .AddCommands(typeof(Program).Assembly)
                        .AddService<IdGeneratorService>()
                        .AddService<NicknameLookupService>()
                        .AddSingleton<Mappers.GameMapper>()
                        .AddHostedServiceEx<ServerlistService>()
                        .AddHostedServiceEx<GameDataService>()
                        .AddHostedServiceEx<ChannelService>()
                        .AddHostedServiceEx<ClanManager>()
                        .AddHostedServiceEx<IpcService>()
                        .AddHostedServiceEx<PlayerSaveService>()
                        .AddHostedServiceEx<PlayerSaveFlushService>()
                        .AddHostedServiceEx<CommandService>();

                    pluginHost.OnConfigure(services);
                });

            var host = hostBuilder.Build();

            var contexts = host.Services.GetRequiredService<IEnumerable<DbContext>>();
            foreach (var db in contexts)
            {
                Log.Information("Checking database={Context}...", db.GetType().Name);

                using (db)
                {
                    if (db.Database.GetPendingMigrations().Any())
                    {
                        if (appOptions.Database.RunMigration)
                        {
                            Log.Information("Applying database={Context} migrations...", db.GetType().Name);
                            db.Database.Migrate();
                        }
                        else
                        {
                            Log.Error("Database={Context} does not have all migrations applied", db.GetType().Name);
                            return;
                        }
                    }
                }
            }

            host.Services
                .GetRequiredService<IProudNetServerService>()
                .UnhandledRmi += (s, e) => Log.Debug("Unhandled Message={@Message} HostId={HostId}", e.Message, e.Session.HostId);

            host.Services.GetRequiredService<IApplicationLifetime>().ApplicationStarted.Register(() =>
                Log.Information("Press Ctrl + C to shutdown"));
            host.Run();
            host.Dispose();
            pluginHost.Dispose();
        }
    }
}
