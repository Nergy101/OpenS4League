using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Buffers;
using OpenS4L.Blub.IO;
using OpenS4L.Blub.Serialization;
using ProudNet.DotNetty.Codecs;
using ProudNet.Serialization;
using ProudNet.Serialization.Messages;
using ProudNet.Serialization.Messages.Core;

namespace ProudNet
{
    /// <summary>
    /// A minimal ProudNet *client* (the game-client side of the wire protocol), used by the
    /// load-bot harness to connect to the servers as if it were a real game client.
    ///
    /// The servers ship only the server-side reimplementation of ProudNet (ProudSession,
    /// ProudNetServerBuilder, DotNetty pipeline). This class implements the client half over a
    /// plain TCP socket, reusing the same shared protocol building blocks that live in this
    /// assembly: <see cref="Crypt"/> (AES/RC4), the core message types, the core
    /// encoder/decoder, and the <see cref="MessageFactory"/> dispatch. Wire-compatibility is
    /// therefore guaranteed by construction against the in-repo server.
    ///
    /// Handshake (mirrors ProudNet.Handlers.AuthenticationHandler on the server):
    ///   1. server -> client  NotifyServerConnectionHintMessage (RSA public key + config)
    ///   2. client generates an AES "secure key" + an RC4 "fast key"
    ///   3. client -> server  NotifyCSEncryptedSessionKeyMessage  (RSA-OAEP(secureKey), AES(fastKey))
    ///   4. server -> client  NotifyCSSessionKeySuccessMessage
    ///   5. client -> server  NotifyServerConnectionRequestDataMessage (network version GUID)
    ///   6. server -> client  NotifyServerConnectSuccessMessage (HostId)
    /// After that, application RMIs flow as encrypted RmiMessage frames.
    /// </summary>
    public class ProudNetClient : IDisposable
    {
        private readonly BlubSerializer _serializer;
        private readonly MessageFactory[] _userFactories;
        private readonly object _writeLock = new object();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private TcpClient _tcp;
        private NetworkStream _stream;
        private Crypt _crypt;
        private bool _disposed;

        /// <summary>Host id assigned by the server during the handshake.</summary>
        public uint HostId { get; private set; }

        public bool IsConnected { get; private set; }

        /// <summary>Raised for every decoded application RMI received from the server.</summary>
        public event EventHandler<MessageEventArgs> MessageReceived;

        public event EventHandler<ProudNetClient> Connected;
        public event EventHandler<ProudNetClient> Disconnected;
        public event EventHandler<Exception> Error;

        /// <summary>Optional protocol trace hook (handshake steps, sends, decoded messages).</summary>
        public Action<string> Trace { get; set; }

        public ProudNetClient(BlubSerializer serializer, IEnumerable<MessageFactory> userFactories)
        {
            _serializer = serializer;
            _userFactories = userFactories.ToArray();
        }

        /// <summary>Connect and complete the ProudNet handshake. <paramref name="networkVersion"/>
        /// must equal the server's NetworkOptions.Version GUID (e.g. the game server's
        /// {beb92241-8333-4117-ab92-9b4af78c688f}).</summary>
        public async Task ConnectAsync(IPEndPoint endpoint, Guid networkVersion, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            _tcp = new TcpClient { NoDelay = true };
            await _tcp.ConnectAsync(endpoint.Address, endpoint.Port).WaitAsync(ct);

            try
            {
                _stream = _tcp.GetStream();

                // 1. server -> client NotifyServerConnectionHintMessage (RSA public key + config)
                var hint = (NotifyServerConnectionHintMessage)await ReadCoreAsync(ct);
                Trace?.Invoke($"handshake: got connection hint (key len {hint.PublicKey.Modulus?.Length * 8} bit)");

                // 2. generate the secure (AES) + fast (RC4) session keys
                var secureKey = new byte[hint.Config.EncryptedMessageKeyLength / 8];
                var fastKey = new byte[16];
                RandomNumberGenerator.Fill(secureKey);
                RandomNumberGenerator.Fill(fastKey);

                _crypt = new Crypt(secureKey);
                _crypt.InitializeFastEncryption(fastKey);

                // 3. RSA-OAEP the secure key; AES-ECB encrypt the fast key with the secure key
                byte[] encSecureKey;
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(hint.PublicKey);
                    encSecureKey = rsa.Encrypt(secureKey, RSAEncryptionPadding.OaepSHA1);
                }

                var aesEncryptor = _crypt.AES.CreateEncryptor();
                var encFastKey = aesEncryptor.TransformFinalBlock(fastKey, 0, fastKey.Length);

                await SendCoreAsync(new NotifyCSEncryptedSessionKeyMessage
                {
                    SecureKey = encSecureKey,
                    FastKey = encFastKey
                }, ct);
                Trace?.Invoke("handshake: sent encrypted session key");

                // 4. server -> client NotifyCSSessionKeySuccessMessage
                await ReadCoreAsync(ct);
                Trace?.Invoke("handshake: session key accepted");

                // 5. client -> server NotifyServerConnectionRequestDataMessage
                await SendCoreAsync(new NotifyServerConnectionRequestDataMessage
                {
                    UserData = Array.Empty<byte>(),
                    Version = networkVersion,
                    InternalNetVersion = Constants.NetVersion
                }, ct);

                // 6. server -> client NotifyServerConnectSuccessMessage (HostId)
                var success = (NotifyServerConnectSuccessMessage)await ReadCoreAsync(ct);
                HostId = success.HostId;
                IsConnected = true;
                Trace?.Invoke($"handshake: connected, hostId={HostId}");
                Connected?.Invoke(this, this);

                _ = Task.Run(ReadLoopAsync, CancellationToken.None);
                // Keep the connection alive: the server's IdleTimeout (~900ms) closes sessions
                // with no inbound traffic, which would otherwise drop a quiet bot mid-flow (e.g.
                // a chat bot between game login and chat login). The real client pings too.
                _ = Task.Run(KeepAliveLoopAsync, CancellationToken.None);
            }
            catch
            {
                _tcp?.Close();
                throw;
            }
        }

        private async Task KeepAliveLoopAsync()
        {
            try
            {
                while (IsConnected)
                {
                    // Send well under the server's ~900ms IdleTimeout so a quiet connection
                    // (e.g. a chat bot between game login and chat login) never gets closed.
                    await Task.Delay(TimeSpan.FromMilliseconds(500), _cts.Token);
                    if (IsConnected)
                        Send(new ReliablePingMessage(), SendOptions.Reliable);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                // Connection closed concurrently; nothing to do.
            }
        }

        /// <summary>Send an application RMI to the server (ReliableSecure by default).</summary>
        public void Send(object message, SendOptions options = null)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected");

            options ??= SendOptions.ReliableSecure;

            var type = message.GetType();
            var isInternal = RmiMessageFactory.Default.ContainsType(type);
            var factory = isInternal
                ? RmiMessageFactory.Default
                : _userFactories.FirstOrDefault(f => f.ContainsType(type));

            if (factory == null)
                throw new ProudException($"No {nameof(MessageFactory)} found for message {type.FullName}");

            var opCode = factory.GetOpCode(type);

            byte[] body;
            using (var ms = new MemoryStream())
            using (var w = ms.ToBinaryWriter(false))
            {
                w.Write(opCode);
                _serializer.Serialize(w, message);
                body = ms.ToArray();
            }

            ICoreMessage core = new RmiMessage(body);

            if (options.Encrypt)
            {
                var data = CoreMessageEncoder.Encode(_serializer, core);
                using var src = new MemoryStream(data);
                using var dst = new MemoryStream();
                _crypt.Encrypt(UnpooledByteBufferAllocator.Default, EncryptMode.Secure, src, dst, true);
                core = new EncryptedReliableMessage(dst.ToArray(), EncryptMode.Secure);
            }

            var framePayload = CoreMessageEncoder.Encode(_serializer, core);
            WriteFrame(framePayload);
            Trace?.Invoke($"send: {type.Name} (encrypt={options.Encrypt}, {framePayload.Length} bytes)");
        }

        private async Task ReadLoopAsync()
        {
            try
            {
                while (IsConnected)
                {
                    var payload = await ReadFrameAsync(_cts.Token);
                    if (payload == null)
                        break;

                    var core = CoreMessageDecoder.Decode(_serializer, Unpooled.WrappedBuffer(payload));
                    Trace?.Invoke($"recv: core {core.GetType().Name}");

                    try
                    {
                        switch (core)
                        {
                            case EncryptedReliableMessage enc:
                                DispatchApp(DecryptToRmi(enc.Data));
                                break;

                            case Encrypted_UnReliableMessage encU:
                                DispatchApp(DecryptToRmi(encU.Data));
                                break;

                            case RmiMessage rmi:
                                DispatchApp(rmi.Data);
                                break;

                            case CompressedMessage compressed:
                                DispatchApp(DecompressToRmi(compressed));
                                break;

                            case ReliablePingMessage:
                                // Internal ping from the server -> answer with a pong (unencrypted).
                                Send(new ReliablePongMessage(), SendOptions.Reliable);
                                break;

                            case UnreliablePingMessage ping:
                                Send(new UnreliablePongMessage(ping.ClientTime, 0), SendOptions.Reliable);
                                break;

                            case ShutdownTcpMessage:
                                Close();
                                return;

                            case NotifyServerDeniedConnectionMessage:
                                Close();
                                return;
                        }
                    }
                    catch (Exception ex)
                    {
                        // A single undecodable/unsupported message must not kill the connection.
                        Error?.Invoke(this, ex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
            }
            finally
            {
                Close();
            }
        }

        /// <summary>Decrypt an EncryptedReliable payload and return the wrapped RmiMessage's data
        /// (the app opcode + serialized body).</summary>
        private byte[] DecryptToRmi(byte[] data)
        {
            using var src = new MemoryStream(data);
            using var dst = new MemoryStream();
            _crypt.Decrypt(UnpooledByteBufferAllocator.Default, EncryptMode.Secure, src, dst, true);

            var core = CoreMessageDecoder.Decode(_serializer, Unpooled.WrappedBuffer(dst.ToArray()));
            if (core is RmiMessage rmi)
                return rmi.Data;

            throw new ProudException($"Expected RmiMessage after decrypt, got {core.GetType().Name}");
        }

        private byte[] DecompressToRmi(CompressedMessage compressed)
        {
            var core = CoreMessageDecoder.Decode(_serializer,
                Unpooled.WrappedBuffer(compressed.Data.DecompressZLib()));
            if (core is RmiMessage rmi)
                return rmi.Data;

            throw new ProudException($"Expected RmiMessage after decompress, got {core.GetType().Name}");
        }

        private void DispatchApp(byte[] rmiData)
        {
            using var ms = new MemoryStream(rmiData);
            using var r = ms.ToBinaryReader(false);

            var opCode = r.ReadUInt16();
            var isInternal = opCode >= 64000;
            var factory = isInternal
                ? RmiMessageFactory.Default
                : _userFactories.FirstOrDefault(f => f.ContainsOpCode(opCode));

            if (factory == null)
                throw new ProudException($"No {nameof(MessageFactory)} found for opcode {opCode}");

            var message = factory.GetMessage(_serializer, opCode, r);
            Trace?.Invoke($"recv: app {message.GetType().Name} (opcode {opCode})");
            MessageReceived?.Invoke(this, new MessageEventArgs(this, message));
        }

        /// <summary>Read one complete frame during the handshake and decode it as a core message.</summary>
        private async Task<ICoreMessage> ReadCoreAsync(CancellationToken ct)
        {
            var payload = await ReadFrameAsync(ct);
            if (payload == null)
                throw new ProudException("Connection closed during handshake");

            return CoreMessageDecoder.Decode(_serializer, Unpooled.WrappedBuffer(payload));
        }

        private void SendCore(ICoreMessage message)
        {
            var framePayload = CoreMessageEncoder.Encode(_serializer, message);
            WriteFrame(framePayload);
        }

        private async Task SendCoreAsync(ICoreMessage message, CancellationToken ct)
        {
            var framePayload = CoreMessageEncoder.Encode(_serializer, message);
            await WriteFrameAsync(framePayload, ct);
        }

        /// <summary>Write one ProudNet frame: [magic(2)][scalar length][payload].</summary>
        private void WriteFrame(byte[] payload)
        {
            var frame = BuildFrame(payload);
            lock (_writeLock)
            {
                _stream.Write(frame, 0, frame.Length);
                _stream.Flush();
            }
        }

        private async Task WriteFrameAsync(byte[] payload, CancellationToken ct)
        {
            var frame = BuildFrame(payload);
            await _stream.WriteAsync(frame, 0, frame.Length, ct);
            await _stream.FlushAsync(ct);
        }

        private static byte[] BuildFrame(byte[] payload)
        {
            using var ms = new MemoryStream();
            using (var w = ms.ToBinaryWriter(false))
            {
                w.Write((short)Constants.NetMagic);
                w.WriteScalar(payload.Length);
                w.Write(payload);
            }

            return ms.ToArray();
        }

        /// <summary>Read one frame's payload. Returns null on clean EOF.</summary>
        private async Task<byte[]> ReadFrameAsync(CancellationToken ct)
        {
            var magic = new byte[2];
            if (!await ReadExactlyAsync(magic, 2, ct))
                return null;

            var prefixBuf = new byte[1];
            if (!await ReadExactlyAsync(prefixBuf, 1, ct))
                return null;

            var prefix = prefixBuf[0];
            var lenBytes = new byte[prefix];
            if (!await ReadExactlyAsync(lenBytes, prefix, ct))
                return null;

            long length = prefix switch
            {
                1 => lenBytes[0],
                2 => BitConverter.ToUInt16(lenBytes, 0),
                4 => BitConverter.ToUInt32(lenBytes, 0),
                _ => throw new ProudException($"Invalid scalar prefix {prefix}")
            };

            if (length <= 0 || length > 65536)
                throw new ProudException($"Invalid frame length {length}");

            var payload = new byte[length];
            if (!await ReadExactlyAsync(payload, (int)length, ct))
                return null;

            return payload;
        }

        private async Task<bool> ReadExactlyAsync(byte[] buffer, int count, CancellationToken ct)
        {
            int read = 0;
            while (read < count)
            {
                int n = await _stream.ReadAsync(buffer, read, count - read, ct);
                if (n == 0)
                    return false;
                read += n;
            }

            return true;
        }

        public void Close()
        {
            if (!IsConnected && _disposed)
                return;

            IsConnected = false;
            _cts.Cancel();
            _stream?.Close();
            _tcp?.Close();
            Disconnected?.Invoke(this, this);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Close();
            _cts.Dispose();
            _crypt?.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ProudNetClient));
        }
    }

    public class MessageEventArgs : EventArgs
    {
        public ProudNetClient Client { get; }
        public object Message { get; }

        public MessageEventArgs(ProudNetClient client, object message)
        {
            Client = client;
            Message = message;
        }
    }
}
