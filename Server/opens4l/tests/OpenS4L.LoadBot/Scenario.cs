using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenS4L.LoadBot
{
    /// <summary>Describes which channel a single bot should enter, and its unique identity.</summary>
    public class BotSpec
    {
        public int Index { get; init; }
        public string Username { get; init; }
        public string Nickname { get; init; }
        public uint Channel { get; init; }
        /// <summary>When set, the bot also logs into the chat server (28003) and chats.</summary>
        public IPEndPoint ChatEndpoint { get; init; }
        /// <summary>When true, skip the game-channel entry (chat-only bots).</summary>
        public bool ChatOnly { get; init; }
        /// <summary>How many chat messages the bot sends (0 = loop until cancelled).</summary>
        public int ChatMessages { get; init; }
    }

    /// <summary>
    /// A load-test scenario: turns a bot count / connection config into a concrete list of
    /// <see cref="BotSpec"/>s. New test cases implement this (e.g. Phase 3 chats/games) and are
    /// selected by name via --scenario.
    /// </summary>
    public interface IScenario
    {
        string Name { get; }
        string Description { get; }
        IReadOnlyList<BotSpec> Create(ScenarioContext ctx);
    }

    /// <summary>Shared inputs a scenario uses to build its bot list.</summary>
    public class ScenarioContext
    {
        public IPEndPoint AuthEndPoint { get; set; } = new IPEndPoint(IPAddress.Loopback, 28002);
        public IPEndPoint GameEndPoint { get; set; }
        public string Pass { get; set; } = "admin";
        public string UserPrefix { get; set; }
        public string NickPrefix { get; set; } = "bot";
        public int StaySeconds { get; set; }
        public string WebApiBase { get; set; }
    }

    /// <summary>Default scenario: N bots all into one channel (the original --count/--channel behaviour).</summary>
    public class SingleChannelScenario : IScenario
    {
        public string Name => "single-channel";
        public string Description => "N bots all into one channel";

        private readonly uint _channel;
        private readonly int _count;

        public SingleChannelScenario(uint channel, int count)
        {
            _channel = channel;
            _count = count;
        }

        public IReadOnlyList<BotSpec> Create(ScenarioContext ctx)
            => Enumerable.Range(0, _count).Select(i => new BotSpec
            {
                Index = i,
                Username = ctx.UserPrefix != null ? $"{ctx.UserPrefix}{i}" : "admin",
                Nickname = $"{ctx.NickPrefix}{i}",
                Channel = _channel
            }).ToArray();
    }

    /// <summary>
    /// Scenario: P players in every channel the game server exposes (via the WebApi /channels).
    /// One bot per (channel, slot) pair, so the dashboard shows every channel populated.
    /// </summary>
    public class AllChannelsScenario : IScenario
    {
        public string Name => "all-channels";
        public string Description => "P players in every channel (one bot per channel)";

        private readonly int _playersPerChannel;

        public AllChannelsScenario(int playersPerChannel)
        {
            _playersPerChannel = playersPerChannel;
        }

        public IReadOnlyList<BotSpec> Create(ScenarioContext ctx)
        {
            var channels = AllChannelsScenario.FetchChannels(ctx.WebApiBase);
            if (channels.Count == 0)
                throw new InvalidOperationException("No channels returned by the WebApi — is the game server up?");

            var specs = new List<BotSpec>();
            var idx = 0;
            foreach (var channel in channels)
            {
                for (var slot = 0; slot < _playersPerChannel; slot++)
                {
                    specs.Add(new BotSpec
                    {
                        Index = idx,
                        Username = ctx.UserPrefix != null ? $"{ctx.UserPrefix}{idx}" : "admin",
                        Nickname = $"{ctx.NickPrefix}{idx}",
                        Channel = channel
                    });
                    idx++;
                }
            }

            return specs;
        }

        internal static List<uint> FetchChannels(string webApiBase)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var json = http.GetStringAsync($"{webApiBase}/channels").GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var ids = new List<uint>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("Id", out var idProp) && idProp.TryGetUInt32(out var id))
                    ids.Add(id);
            }

            return ids;
        }
    }

    /// <summary>
    /// Scenario: N bots that log into the chat server and send channel chat. Each bot does the
    /// auth + game login first (so its player record + nickname exist — chat login requires them),
    /// then opens a chat connection, logs in, and sends a message every few seconds.
    /// </summary>
    public class ChatScenario : IScenario
    {
        public string Name => "chat";
        public string Description => "N bots chatting on the chat server";

        private readonly int _count;
        private readonly IPEndPoint _chatEndPoint;

        public ChatScenario(int count, IPEndPoint chatEndPoint)
        {
            _count = count;
            _chatEndPoint = chatEndPoint;
        }

        public IReadOnlyList<BotSpec> Create(ScenarioContext ctx)
            => Enumerable.Range(0, _count).Select(i => new BotSpec
            {
                Index = i,
                Username = ctx.UserPrefix != null ? $"{ctx.UserPrefix}{i}" : "admin",
                Nickname = $"{ctx.NickPrefix}{i}",
                Channel = 1,
                ChatEndpoint = _chatEndPoint,
                ChatOnly = true
            }).ToArray();
    }

    /// <summary>
    /// Scenario: P players in every channel AND every player sends N chat messages over the chat
    /// server. Each bot enters its assigned game channel (required for chat login) and then sends
    /// N channel-chat messages.
    /// </summary>
    public class AllChannelsChatScenario : IScenario
    {
        public string Name => "all-channels-chat";
        public string Description => "P players in every channel, each sending N chat messages";

        private readonly int _playersPerChannel;
        private readonly int _messagesPerBot;
        private readonly IPEndPoint _chatEndPoint;

        public AllChannelsChatScenario(int playersPerChannel, int messagesPerBot, IPEndPoint chatEndPoint)
        {
            _playersPerChannel = playersPerChannel;
            _messagesPerBot = messagesPerBot;
            _chatEndPoint = chatEndPoint;
        }

        public IReadOnlyList<BotSpec> Create(ScenarioContext ctx)
        {
            var channels = AllChannelsScenario.FetchChannels(ctx.WebApiBase);
            if (channels.Count == 0)
                throw new InvalidOperationException("No channels returned by the WebApi — is the game server up?");

            var specs = new List<BotSpec>();
            var idx = 0;
            foreach (var channel in channels)
            {
                for (var slot = 0; slot < _playersPerChannel; slot++)
                {
                    specs.Add(new BotSpec
                    {
                        Index = idx,
                        Username = ctx.UserPrefix != null ? $"{ctx.UserPrefix}{idx}" : "admin",
                        Nickname = $"{ctx.NickPrefix}{idx}",
                        Channel = channel,
                        ChatEndpoint = _chatEndPoint,
                        ChatMessages = _messagesPerBot
                    });
                    idx++;
                }
            }

            return specs;
        }
    }
}
