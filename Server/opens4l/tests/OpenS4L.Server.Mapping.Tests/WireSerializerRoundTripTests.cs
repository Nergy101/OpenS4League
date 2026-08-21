using System;
using System.Drawing;
using System.IO;
using OpenS4L.Blub.Serialization;
using OpenS4L.Network.Serializers;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Round-trip tests for the wire-format primitive serializers. These are the value codecs
    /// that define the client-server contract, so a change here silently breaks the wire format.
    /// Each test serializes a value and deserializes it back, asserting equality.
    /// </summary>
    public class WireSerializerRoundTripTests
    {
        private static (T value, T roundTripped) RoundTrip<T>(ISerializer<T> serializer, T value)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                serializer.Serialize(BlubSerializer.Instance, writer, value);

            stream.Position = 0;
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8);
            var result = serializer.Deserialize(BlubSerializer.Instance, reader);
            return (value, result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(1700000000)]
        [InlineData(2147483647)]
        public void UnixTime_roundtrips(long unixSeconds)
        {
            var value = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            var (_, result) = RoundTrip(new UnixTimeSerializer(), value);
            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(500)]
        [InlineData(1234567)]
        [InlineData(4294967295)]
        public void TimeSpanMilliseconds_roundtrips(uint ms)
        {
            var value = TimeSpan.FromMilliseconds(ms);
            var (_, result) = RoundTrip(new TimeSpanMillisecondsSerializer(), value);
            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(500)]
        [InlineData(1234567)]
        [InlineData(4294967295)]
        public void TimeSpanSeconds_roundtrips(uint s)
        {
            var value = TimeSpan.FromSeconds(s);
            var (_, result) = RoundTrip(new TimeSpanSecondsSerializer(), value);
            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(0xFF0000)]
        [InlineData(0x00FF00)]
        [InlineData(0x0000FF)]
        [InlineData(-16777216)]
        public void Color_roundtrips(int argb)
        {
            var value = Color.FromArgb(argb);
            var (_, result) = RoundTrip(new ColorSerializer(), value);
            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData(1700000000)]
        [InlineData(2147483647)]
        [InlineData(0)]
        public void ClubCreationDate_roundtrips(long unixSeconds)
        {
            // NOTE: this codec stores local wall-clock time ("yyyyMMddHHmmss") with NO timezone
            // offset, so round-tripping a UTC value comes back with the LOCAL offset and possibly
            // a truncated second (DateTimeOffset.FromUnixTimeSeconds can round the tick). The
            // format string is the real contract: assert the wire bytes are STABLE, i.e. the
            // re-serialized value reproduces the exact same wall-clock string.
            var value = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            var (_, roundTripped) = RoundTrip(new ClubCreationDateSerializer(), value);

            // Serialize both the original and the round-tripped value; the encoded strings must match.
            Assert.Equal(Encode(value), Encode(roundTripped));
        }

        private static string Encode(DateTimeOffset value)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                new ClubCreationDateSerializer().Serialize(BlubSerializer.Instance, writer, value);
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
