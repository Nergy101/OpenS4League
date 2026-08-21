using System.Net;
using System.Reflection;
using Logging;
using OpenS4L.Server.Chat;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Core transport-fake harness: builds a real ProudNet Session over a FakeSocketChannel
    /// (no network), so message handlers can be driven with a real session in-process.
    /// </summary>
    public class TransportHarnessTests
    {
        [Fact]
        public void ChatSession_constructsOverFakeChannel()
        {
            var channel = new FakeSocketChannel(new IPEndPoint(IPAddress.Loopback, 21000));
            var session = new Session(new Logger<Session>(), 1, channel);
            Assert.True(session.IsConnected);
            Assert.Equal(1u, session.HostId);
        }

        [Fact]
        public void GameSession_constructsOverFakeChannel()
        {
            var channel = new FakeSocketChannel(new IPEndPoint(IPAddress.Loopback, 21000));
            var session = new OpenS4L.Server.Game.Session(new Logger<OpenS4L.Server.Game.Session>(), 2, channel);
            Assert.True(session.IsConnected);
            Assert.Equal(2u, session.HostId);
        }

        [Fact]
        public void ChatSession_send_capturesOutbound()
        {
            var channel = new FakeSocketChannel(new IPEndPoint(IPAddress.Loopback, 21000));
            var session = new Session(new Logger<Session>(), 1, channel);

            // Send a marker message and verify it was handed to the (fake) channel.
            var marker = new object();
            session.Send(marker);
            Assert.Single(channel.Outbound);
            var sent = channel.Outbound[0];
            var messageProp = sent.GetType().GetProperty("Message", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.Equal(marker, messageProp!.GetValue(sent));
        }
    }
}
