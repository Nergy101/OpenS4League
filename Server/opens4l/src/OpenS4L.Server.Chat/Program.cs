using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNetty.Transport.Channels;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Serializer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenS4L.Common;
using OpenS4L.Common.Configuration;
using OpenS4L.Common.Messaging;
using OpenS4L.Common.Plugins;
using OpenS4L.Database;
using OpenS4L.Network.Data.Chat;
using OpenS4L.Network.Message.Chat;
using OpenS4L.Network.Serializers;
using OpenS4L.Server.Chat.Services;
using Newtonsoft.Json;
using ProudNet;
using ProudNet.Hosting;
using ProudNet.Hosting.Services;
using Serilog;
using StackExchange.Redis;

namespace OpenS4L.Server.Chat
{
    internal static class Program
    {
        private static void Main()
        {
            var baseDirectory = Environment.GetEnvironmentVariable("OPENS4L_BASEDIR_CHAT");
            if (string.IsNullOrWhiteSpace(baseDirectory))
                baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            var configuration = Startup.Initialize(baseDirectory, "config.hjson",
                x => x.GetSection(nameof(AppOptions.Logging)).Get<LoggerOptions>());

            Log.Information("Starting...");

            var appOptions = configuration.Get<AppOptions>();
            var hostBuilder = new HostBuilder();
            var redisConnectionMultiplexer = ConnectionMultiplexer.Connect(appOptions.Database.ConnectionStrings.Redis);

            IPluginHost pluginHost = new ScanPluginHost();
            pluginHost.Initialize(configuration, Path.Combine(baseDirectory, "plugins"));

            hostBuilder
                .ConfigureHostConfiguration(builder => builder.AddConfiguration(configuration))
                .ConfigureAppConfiguration(builder => builder.AddConfiguration(configuration))
                .UseConsoleLifetime()
                .UseProudNetServer(builder =>
                {
                    var messageHandlerResolver = new DefaultMessageHandlerResolver(
                        AppDomain.CurrentDomain.GetAssemblies(), typeof(IChatMessage));

                    builder
                        .UseHostIdFactory<HostIdFactory>()
                        .UseSessionFactory<SessionFactory>()
                        .AddMessageFactory<ChatMessageFactory>()
                        .UseMessageHandlerResolver(messageHandlerResolver)
                        .UseNetworkConfiguration((context, options) =>
                        {
                            options.Version = new Guid("{97d36acf-8cc0-4dfb-bcc9-97cab255e2bc}");
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
                            serializer.AddSerializer(new ItemNumberSerializer());
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
                        .Configure<MetricsOptions>(context.Configuration.GetSection(nameof(AppOptions.Metrics)))
                        .Configure<IdGeneratorOptions>(x => x.Id = 1)
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
                        .AddTransient<Player>()
                        .AddTransient<Mailbox>()
                        .AddTransient<DenyManager>()
                        .AddTransient<FriendManager>()
                        .AddTransient<PlayerSettingManager>()
                        .AddSingleton<PlayerManager>()
                        .AddSingleton<ChannelManager>()
                        .AddService<IdGeneratorService>()
                        .AddSingleton(x => new Mappers.ChatMapper(x.GetRequiredService<IOptions<ServerListOptions>>().Value.Id))
                        .AddHostedServiceEx<ServerlistService>()
                        .AddHostedServiceEx<IpcService>()
                        .AddHostedServiceEx<ChatMetricsService>();

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
