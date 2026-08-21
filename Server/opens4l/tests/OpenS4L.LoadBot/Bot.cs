using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenS4L;
using OpenS4L.Blub.Serialization;
using OpenS4L.Network;
using OpenS4L.Network.Data.Auth;
using OpenS4L.Network.Message.Auth;
using OpenS4L.Network.Message.Chat;
using OpenS4L.Network.Message.Game;
using ProudNet;
using ProudNet.Serialization;

namespace OpenS4L.LoadBot
{
    /// <summary>
    /// One simulated player: connects to the auth server, logs in, connects to the game server,
    /// and enters a channel. A real (network-valid) ProudNet client on the wire, so the bot shows
    /// up in the game server's PlayerManager and thus in the admin console (/players, /channels).
    /// </summary>
    public class Bot
    {
        // ProudNet "network version" GUID each server validates during the handshake.
        private static readonly Guid AuthVersion = new Guid("{9be73c0b-3b10-403e-be7d-9f222702a38c}");
        private static readonly Guid GameVersion = new Guid("{beb92241-8333-4117-ab92-9b4af78c688f}");
        private static readonly Guid ChatVersion = new Guid("{97d36acf-8cc0-4dfb-bcc9-97cab255e2bc}");

        private static readonly Version ClientVersion = new Version(0, 8, 32, 26995);

        private readonly BlubSerializer _serializer;
        private readonly MessageFactory[] _factories;
        private readonly IPEndPoint _authEndPoint;
        private readonly IPEndPoint _gameEndPoint;
        private readonly IPEndPoint _chatEndPoint;
        private readonly string _username;
        private readonly string _password;
        private readonly uint _channelId;
        private readonly string _nickname;
        private readonly int _chatMessages;

        public int Index { get; }

        /// <summary>Optional per-bot protocol trace (handshake steps, sends, decoded messages).</summary>
        public Action<string> Trace { get; set; }

        public Bot(BlubSerializer serializer, MessageFactory[] factories, int index,
            IPEndPoint authEndPoint, IPEndPoint gameEndPoint, string username, string password,
            uint channelId, string nickname, IPEndPoint chatEndPoint = null, int chatMessages = 0)
        {
            _serializer = serializer;
            _factories = factories;
            Index = index;
            _authEndPoint = authEndPoint;
            _gameEndPoint = gameEndPoint;
            _chatEndPoint = chatEndPoint;
            _username = username;
            _password = password;
            _channelId = channelId;
            _nickname = nickname;
            _chatMessages = chatMessages;
        }

        public async Task<bool> RunAsync(CancellationToken ct)
        {
            using var auth = new ServerConnection(_serializer, _factories);
            auth.Client.Trace = Trace;
            auth.Client.Error += (_, ex) => Log("auth error: {0}", ex.Message);
            auth.Client.Disconnected += (_, __) => Log("auth disconnected");
            Log("connecting to auth {0}", _authEndPoint);

            await auth.Client.ConnectAsync(_authEndPoint, AuthVersion, ct);

            // 1. Auth login
            auth.Client.Send(new LoginEUReqMessage
            {
                Username = _username,
                Password = _password,
                Unk1 = "", Unk2 = "", Unk3 = 0, Unk4 = 0, Unk5 = 0, Unk6 = "", Unk7 = 0,
                Unk8 = "", Unk9 = "", Token = new AeriaTokenDto(), Unk10 = ""
            });

            var loginAck = await auth.WaitFor<LoginEUAckMessage>(TimeSpan.FromSeconds(10));
            if (loginAck.Result != AuthLoginResult.OK)
            {
                Log("auth login rejected: {0}", loginAck.Result);
                return false;
            }

            Log("auth login OK (account {0}, session {1})", loginAck.AccountId, loginAck.SessionId);

            // 2. Server list -> find the game server endpoint.
            // After a game-server restart it can take up to ~30s for it to announce itself to
            // auth (the server-list update interval). Re-request until it appears so a test run
            // right after a restart doesn't fail with "no game server in server list".
            var gameInfo = null as ServerInfoDto;
            var serverListDeadline = DateTime.UtcNow.AddSeconds(60);
            while (gameInfo == null && DateTime.UtcNow < serverListDeadline)
            {
                auth.Client.Send(new ServerListReqMessage());
                var serverList = await auth.WaitFor<ServerListAckMessage>(TimeSpan.FromSeconds(10));
                foreach (var s in serverList.ServerList)
                {
                    if (s.Type == ServerType.Game && s.IsEnabled)
                    {
                        gameInfo = s;
                        break;
                    }
                }

                if (gameInfo == null)
                {
                    Log("game server not in server list yet, retrying...");
                    await Task.Delay(3000, ct);
                }
            }

            if (gameInfo == null)
            {
                Log("no game server in server list after retries");
                return false;
            }

            Log("server list has game server {0} at {1}", gameInfo.Name, gameInfo.EndPoint);
            var gameEndPoint = _gameEndPoint ?? gameInfo.EndPoint;

            // 3. Game login
            using var game = new ServerConnection(_serializer, _factories);
            game.Client.Trace = Trace;
            game.Client.Error += (_, ex) => Log("game error: {0}", ex.Message);
            game.Client.Disconnected += (_, __) => Log("game disconnected");
            game.Client.MessageReceived += (_, e) =>
            {
                if (e.Message is ServerResultAckMessage res)
                    Log("game result: {0}", res.Result);
                else if (e.Message is ChannelListInfoAckMessage)
                    Log("game result: got channel list");
            };
            Log("connecting to game {0}", gameEndPoint);
            await game.Client.ConnectAsync(gameEndPoint, GameVersion, ct);

            game.Client.Send(new LoginRequestReqMessage
            {
                Username = _username,
                Version = ClientVersion,
                AccountId = loginAck.AccountId,
                SessionId = loginAck.SessionId,
                KickConnection = true,
                Unk1 = 0, Unk3 = 0, Unk4 = "", Unk5 = "", Unk6 = 0,
                AeriaToken = new AeriaTokenDto()
            });

            var gameAck = await game.WaitFor<LoginReguestAckMessage>(TimeSpan.FromSeconds(10));
            Log("game login ack result={0}", gameAck.Result);
            if (gameAck.Result == GameLoginResult.ChooseNickname)
            {
                Log("game login: nickname required, creating character '{0}'", _nickname);
                game.Client.Send(new CharacterFirstCreateReqMessage
                {
                    Nickname = _nickname,
                    Style = new CharacterStyle(CharacterGender.Male, 0, 0, 0, 0, 0),
                    // FixedArraySerializer(8): must supply exactly 8 item numbers. The server
                    // auto-grants a weapon (dagger 2000006) + skill (shield 3050001) regardless,
                    // so include them + 6 empty slots.
                    Items = new[]
                    {
                        new ItemNumber(2000006), new ItemNumber(3050001),
                        new ItemNumber(0), new ItemNumber(0), new ItemNumber(0),
                        new ItemNumber(0), new ItemNumber(0), new ItemNumber(0)
                    }
                });
            }
            else if (gameAck.Result != GameLoginResult.OK)
            {
                Log("game login failed: {0}", gameAck.Result);
                return false;
            }

            // Give the login/account-information flow a moment to settle.
            await Task.Delay(500, ct);

            // 4. Enter a channel. This keeps the player registered in the game server's
            // PlayerManager (the all-channels test proves players who enter a channel stay
            // online). Chat login (server-side OnChatLogin) looks the player up there, so a
            // chat bot MUST be in a game channel for chat login to succeed.
            Log("entering channel {0}", _channelId);
            game.Client.Send(new ChannelEnterReqMessage { Channel = _channelId });
            Log("bot {0} online in channel {1}", Index, _channelId);

            // 5. If a chat endpoint is configured, also connect to the chat server and chat.
            if (_chatEndPoint != null)
                return await DoChatAsync(game, loginAck.AccountId, loginAck.SessionId, ct);

            // 6. Otherwise stay connected until cancelled.
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
            }

            Log("bot {0} shutting down", Index);
            return true;
        }

        private async Task<bool> DoChatAsync(ServerConnection game, ulong accountId, string sessionId, CancellationToken ct)
        {
            using var chat = new ServerConnection(_serializer, _factories);
            chat.Client.Trace = Trace;
            chat.Client.Error += (_, ex) => Log("chat error: {0}", ex.Message);
            chat.Client.Disconnected += (_, __) => Log("chat disconnected");

            Log("connecting to chat {0}", _chatEndPoint);
            await chat.Client.ConnectAsync(_chatEndPoint, ChatVersion, ct);

            chat.Client.Send(new LoginReqMessage
            {
                AccountId = accountId,
                Nickname = _nickname,
                SessionId = sessionId
            });

            var ack = await chat.WaitFor<LoginAckMessage>(TimeSpan.FromSeconds(10));
            if (ack.Result != 0)
            {
                Log("chat login failed: result {0}", ack.Result);
                return false;
            }

            Log("chat login OK — chatting as '{0}'", _nickname);

            // Send chat messages. If _chatMessages > 0, send exactly that many then finish;
            // otherwise send one every ~3s until cancelled.
            var n = 0;
            try
            {
                while (_chatMessages == 0 || n < _chatMessages)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                    n++;
                    chat.Client.Send(new MessageChatReqMessage
                    {
                        ChatType = ChatType.Channel,
                        Message = $"hello from bot {Index} ({n})"
                    });
                }
            }
            catch (OperationCanceledException)
            {
            }

            Log("bot {0} chat done ({1} message(s) sent)", Index, n);
            return true;
        }

        private void Log(string message, params object[] args)
        {
            Console.WriteLine($"[bot {Index}] " + string.Format(message, args));
        }
    }
}
