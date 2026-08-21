using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenS4L.Blub.Serialization;
using OpenS4L.Network.Message.Auth;
using OpenS4L.Network.Message.Chat;
using OpenS4L.Network.Message.Club;
using OpenS4L.Network.Message.Game;
using OpenS4L.Network.Message.GameRule;
using OpenS4L.Network.Serializers;
using ProudNet;
using ProudNet.Serialization;
using ProudNet.Serialization.Serializers;

namespace OpenS4L.LoadBot
{
    /// <summary>
    /// OpenS4L load-bot: connect simulated players to the servers over the real ProudNet wire
    /// protocol. Each bot logs into auth, connects to the game server, and enters a channel, so
    /// they appear in the admin console's /players and /channels.
    ///
    /// Usage:
    ///   opens4l-loadbot [--auth host:port] [--game host:port] [--pass p] [--stay seconds]
    ///                   [--scenario name] [scenario args...]
    ///
    /// Scenarios:
    ///   single-channel  (default) N bots into one channel:  --count N --channel id [--user-prefix p]
    ///   all-channels    P players in every channel:        --per-channel P [--user-prefix p]
    ///
    /// Defaults: auth=127.0.0.1:28002, game=<from server list>, pass=admin, stay=0 (forever).
    /// For N concurrent bots you need N accounts: provision them with `make provision-bots BOTS=N`
    /// then pass --user-prefix so each bot logs in as {prefix}{i}.
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var (scenario, ctx, errors) = ParseArgs(args);
            if (errors.Count > 0)
            {
                foreach (var e in errors)
                    Console.Error.WriteLine("error: " + e);
                Console.WriteLine(Usage());
                return 2;
            }

            var specs = scenario.Create(ctx);

            var serializer = BuildSerializer();
            var factories = BuildFactories();

            var cts = new CancellationTokenSource();
            if (ctx.StaySeconds > 0)
                cts.CancelAfter(TimeSpan.FromSeconds(ctx.StaySeconds));

            Console.WriteLine($"OpenS4L load-bot: scenario={scenario.Name} ({scenario.Description}) -> " +
                              $"{specs.Count} bot(s), auth={ctx.AuthEndPoint}");

            var bots = specs.Select(s =>
            {
                var bot = new Bot(
                    serializer,
                    factories,
                    s.Index,
                    ctx.AuthEndPoint,
                    ctx.GameEndPoint,
                    s.Username,
                    ctx.Pass,
                    s.Channel,
                    s.Nickname,
                    s.ChatEndpoint,
                    s.ChatMessages
                );
                bot.Trace = tr => Console.WriteLine($"[bot {s.Index}] TRACE {tr}");
                return bot;
            }).ToArray();

            var tasks = bots.Select(b => RunBot(() => b.RunAsync(cts.Token))).ToArray();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var completed = await Task.WhenAll(tasks);

            var online = completed.Count(r => r);
            Console.WriteLine($"Done. {online}/{specs.Count} bot(s) reached their channel.");
            return online == specs.Count ? 0 : 1;
        }

        private static async Task<bool> RunBot(Func<Task<bool>> run)
        {
            try
            {
                return await run();
            }
            catch (OperationCanceledException)
            {
                return false; // interrupted before reaching the channel (Ctrl-C mid-flow)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"bot error: {ex}");
                return false;
            }
        }

        private static BlubSerializer BuildSerializer()
        {
            var serializer = new BlubSerializer();
            serializer.AddSerializer(new ArrayWithScalarSerializer());
            serializer.AddSerializer(new IPEndPointSerializer());
            serializer.AddSerializer(new StringSerializer());
            serializer.AddSerializer(new ArrayWithIntPrefixSerializer());
            serializer.AddSerializer(new CharacterStyleSerializer());
            serializer.AddSerializer(new ItemNumberSerializer());
            serializer.AddSerializer(new VersionSerializer());
            serializer.AddSerializer(new PeerIdSerializer());
            serializer.AddSerializer(new LongPeerIdSerializer());
            return serializer;
        }

        private static MessageFactory[] BuildFactories() => new MessageFactory[]
        {
            new AuthMessageFactory(),
            new GameMessageFactory(),
            new GameRuleMessageFactory(),
            new ClubMessageFactory(),
            new ChatMessageFactory(),
        };

        private static (IScenario Scenario, ScenarioContext Ctx, List<string> Errors) ParseArgs(string[] args)
        {
            var ctx = new ScenarioContext();
            var errors = new List<string>();

            string Next(int i, string flag)
            {
                if (i + 1 >= args.Length)
                {
                    errors.Add($"{flag} requires a value");
                    return null;
                }

                return args[i + 1];
            }

            var scenarioName = "single-channel";
            var count = 1;
            uint channel = 4;
            var perChannel = 0;
            var messagesPerBot = 0;
            IPEndPoint chatEndPoint = null;

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--auth":
                        var a = Next(i, "--auth"); i++;
                        if (a != null)
                        {
                            if (TryParseEndPoint(a, out var authEp))
                                ctx.AuthEndPoint = authEp;
                            else
                                errors.Add($"invalid --auth endpoint '{a}'");
                        }
                        break;

                    case "--game":
                        var g = Next(i, "--game"); i++;
                        if (g != null)
                        {
                            if (TryParseEndPoint(g, out var gameEp))
                                ctx.GameEndPoint = gameEp;
                            else
                                errors.Add($"invalid --game endpoint '{g}'");
                        }
                        break;

                    case "--pass":
                        ctx.Pass = Next(i, "--pass"); i++;
                        break;

                    case "--chat-endpoint":
                        var ce = Next(i, "--chat-endpoint"); i++;
                        if (ce != null)
                        {
                            if (TryParseEndPoint(ce, out var chatEp))
                                chatEndPoint = chatEp;
                            else
                                errors.Add($"invalid --chat-endpoint '{ce}'");
                        }
                        break;

                    case "--user-prefix":
                        ctx.UserPrefix = Next(i, "--user-prefix"); i++;
                        break;

                    case "--nick-prefix":
                        ctx.NickPrefix = Next(i, "--nick-prefix"); i++;
                        break;

                    case "--stay":
                        var st = Next(i, "--stay"); i++;
                        if (st != null)
                        {
                            if (int.TryParse(st, out var stay))
                                ctx.StaySeconds = stay;
                            else
                                errors.Add($"invalid --stay '{st}'");
                        }
                        break;

                    case "--scenario":
                        var sc = Next(i, "--scenario"); i++;
                        if (sc != null)
                            scenarioName = sc;
                        break;

                    case "--count":
                        var c = Next(i, "--count"); i++;
                        if (c != null)
                        {
                            if (int.TryParse(c, out var n) && n >= 1 && n <= 1000)
                                count = n;
                            else
                                errors.Add($"invalid --count '{c}' (1-1000)");
                        }
                        break;

                    case "--channel":
                        var ch = Next(i, "--channel"); i++;
                        if (ch != null)
                        {
                            if (uint.TryParse(ch, out var id))
                                channel = id;
                            else
                                errors.Add($"invalid --channel '{ch}'");
                        }
                        break;

                    case "--per-channel":
                        var pc = Next(i, "--per-channel"); i++;
                        if (pc != null)
                        {
                            if (int.TryParse(pc, out var p) && p >= 1 && p <= 1000)
                                perChannel = p;
                            else
                                errors.Add($"invalid --per-channel '{pc}' (1-1000)");
                        }
                        break;

                    case "--messages":
                        var msg = Next(i, "--messages"); i++;
                        if (msg != null)
                        {
                            if (int.TryParse(msg, out var m) && m >= 1 && m <= 10000)
                                messagesPerBot = m;
                            else
                                errors.Add($"invalid --messages '{msg}' (1-10000)");
                        }
                        break;

                    default:
                        errors.Add($"unknown argument '{args[i]}'");
                        break;
                }
            }

            if (errors.Count > 0)
                return (null, ctx, errors);

            // WebApi base for scenarios that need the live channel list.
            ctx.WebApiBase = $"http://{ctx.AuthEndPoint.Address}:22000";

            IScenario scenario = scenarioName switch
            {
                "single-channel" => new SingleChannelScenario(channel, count),
                "all-channels" => new AllChannelsScenario(perChannel),
                "chat" => new ChatScenario(count, chatEndPoint ?? new IPEndPoint(IPAddress.Loopback, 28003)),
                "all-channels-chat" => new AllChannelsChatScenario(
                    perChannel, messagesPerBot, chatEndPoint ?? new IPEndPoint(IPAddress.Loopback, 28003)),
                _ => null
            };

            if (scenario == null)
                errors.Add($"unknown scenario '{scenarioName}' (known: single-channel, all-channels, chat, all-channels-chat)");

            return (scenario, ctx, errors);
        }

        private static bool TryParseEndPoint(string s, out IPEndPoint endpoint)
        {
            endpoint = null;
            var parts = s.Split(':');
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var ip) ||
                !int.TryParse(parts[1], out var port))
                return false;

            endpoint = new IPEndPoint(ip, port);
            return true;
        }

        private static string Usage() => @"
Usage: opens4l-loadbot [options] [--scenario name]

Global:
  --auth <host:port>    Auth server endpoint (default 127.0.0.1:28002)
  --game <host:port>    Game server endpoint (default: from the server list)
  --pass <pass>         Account password (default admin)
  --nick-prefix <s>     Nickname prefix (default bot)
  --stay <seconds>      Stay online N seconds then exit (0 = forever, default 0)
  --user-prefix <s>     Each bot logs in as its own account {prefix}{i} (provision with
                        `make provision-bots BOTS=N`). Needed for N concurrent bots.

Scenarios:
  single-channel   N bots into one channel (default):
                   --count N --channel id
  all-channels     P players in every existing channel:
                   --per-channel P
                   (channels are discovered from the WebApi /channels; with 11 channels
                    and P=10 this is 110 bots)
  chat             N bots chatting on the chat server:
                   --count N [--chat-endpoint host:port (default 127.0.0.1:28003)]
                   (each bot also logs into auth+game first so its player/nickname exist)
  all-channels-chat  P players in every channel, each sending M chat messages:
                   --per-channel P --messages M [--chat-endpoint host:port]
                   (with 11 channels and P=10 that's 110 bots, 10 per channel, 10 msgs each)";
    }
}
