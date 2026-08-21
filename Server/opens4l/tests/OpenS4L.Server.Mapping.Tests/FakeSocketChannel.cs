using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Embedded;
using DotNetty.Transport.Channels.Sockets;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Minimal in-memory ISocketChannel for tests. ProudSession's ctor casts IChannel→ISocketChannel
    /// and reads RemoteAddress/LocalAddress (as IPEndPoint); Send() checks IsWritable/Active and calls
    /// WriteAndFlushAsync. This fake implements that surface and captures everything the session "sends",
    /// so handlers can be driven end-to-end with no network. Members the transport never touches throw
    /// NotSupportedException.
    /// </summary>
    public sealed class FakeSocketChannel : ISocketChannel
    {
        private readonly List<object> _outbound = new List<object>();

        /// <summary>Everything passed to WriteAndFlushAsync (raw SendContext objects).</summary>
        public IReadOnlyList<object> Outbound => _outbound;

        /// <summary>The session-facing channel is always connected + writable.</summary>
        public bool Active => true;
        public bool Open => true;
        public bool Registered => true;
        public bool IsWritable => true;

        public IChannelId Id { get; } = new StubChannelId();
        public IByteBufferAllocator Allocator { get; } = new UnpooledByteBufferAllocator();
        public IEventLoop EventLoop { get; set; }
        public IChannel Parent => null;
        public ChannelMetadata Metadata { get; } = new ChannelMetadata(false);
        public EndPoint LocalAddress { get; }
        public EndPoint RemoteAddress { get; }
        public Task CloseCompletion { get; } = Task.CompletedTask;
        public IChannelUnsafe Unsafe => throw new NotSupportedException();
        public IChannelPipeline Pipeline => throw new NotSupportedException();
        public IChannelConfiguration Configuration => throw new NotSupportedException();

        public FakeSocketChannel(IPEndPoint remote, IPEndPoint local = null)
        {
            RemoteAddress = remote;
            LocalAddress = local ?? new IPEndPoint(IPAddress.Any, 0);
        }

        public Task BindAsync(EndPoint localAddress) => Task.CompletedTask;
        public Task ConnectAsync(EndPoint remoteAddress) => Task.CompletedTask;
        public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task DeregisterAsync() => Task.CompletedTask;
        public IChannel Flush() => this;
        public IChannel Read() => this;
        public Task CloseAsync() => Task.CompletedTask;

        public Task WriteAsync(object message)
        {
            _outbound.Add(message);
            return Task.CompletedTask;
        }

        public Task WriteAndFlushAsync(object message)
        {
            _outbound.Add(message);
            return Task.CompletedTask;
        }

        IAttribute<T> IAttributeMap.GetAttribute<T>(AttributeKey<T> key) => throw new NotSupportedException();
        bool IAttributeMap.HasAttribute<T>(AttributeKey<T> key) => false;

        public int CompareTo(IChannel other) => ReferenceEquals(this, other) ? 0 : 1;
    }

    internal sealed class StubChannelId : IChannelId
    {
        public int CompareTo(IChannelId other) => ReferenceEquals(this, other) ? 0 : 1;
        public bool Equals(IChannelId other) => ReferenceEquals(this, other);
        public override bool Equals(object obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => 0;
        public override string ToString() => "stub";
        public string AsShortText() => "stub";
        public string AsLongText() => "stub";
    }
}
