using System;
using System.ComponentModel;
using System.Net;
using System.Text;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Common.Converters;
using OpenS4L.Common.Converters.Json;
using Newtonsoft.Json;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Tests for OpenS4L.Common TypeConverters (used to bind hjson config strings to option
    /// types) and the JSON converters used by the admin/API serialization.
    /// </summary>
    public class CommonConverterTests
    {
        // ---- TypeConverters ----

        [Fact]
        public void ItemNumberTypeConverter_roundtrips()
        {
            var conv = new ItemNumberTypeConverter();
            Assert.True(conv.CanConvertFrom(null, typeof(string)));
            var item = (ItemNumber)conv.ConvertFrom(null, System.Globalization.CultureInfo.InvariantCulture, "2000001");
            Assert.Equal(2000001u, item.Id);
            var id = conv.ConvertTo(null, System.Globalization.CultureInfo.InvariantCulture, item, typeof(ItemNumber));
            Assert.Equal(item.Id, id);
        }

        [Fact]
        public void TimeSpanTypeConverter_roundtrips()
        {
            var conv = new TimeSpanTypeConverter();
            Assert.True(conv.CanConvertFrom(null, typeof(string)));
            var ts = (TimeSpan)conv.ConvertFrom(null, System.Globalization.CultureInfo.InvariantCulture, "1500");
            Assert.Equal(TimeSpan.FromMilliseconds(1500), ts);
            var s = conv.ConvertTo(null, System.Globalization.CultureInfo.InvariantCulture, ts, typeof(string));
            Assert.Equal("1500", s);
        }

        [Fact]
        public void VersionTypeConverter_roundtrips()
        {
            var conv = new VersionTypeConverter();
            var v = (Version)conv.ConvertFrom(null, System.Globalization.CultureInfo.InvariantCulture, "1.2.3");
            Assert.Equal(new Version(1, 2, 3), v);
            var s = conv.ConvertTo(null, System.Globalization.CultureInfo.InvariantCulture, v, typeof(string));
            Assert.Equal("1.2.3", s);
        }

        [Fact]
        public void IPAddressTypeConverter_roundtrips()
        {
            var conv = new IPAddressTypeConverter();
            var ip = (IPAddress)conv.ConvertFrom(null, System.Globalization.CultureInfo.InvariantCulture, "127.0.0.1");
            Assert.Equal(IPAddress.Loopback, ip);
            var s = conv.ConvertTo(null, System.Globalization.CultureInfo.InvariantCulture, ip, typeof(string));
            Assert.Equal("127.0.0.1", s);
        }

        [Fact]
        public void IPEndPointTypeConverter_roundtrips()
        {
            var conv = new IPEndPointTypeConverter();
            var ep = (IPEndPoint)conv.ConvertFrom(null, System.Globalization.CultureInfo.InvariantCulture, "127.0.0.1:22000");
            Assert.Equal(IPAddress.Loopback, ep.Address);
            Assert.Equal(22000, ep.Port);
            var s = conv.ConvertTo(null, System.Globalization.CultureInfo.InvariantCulture, ep, typeof(string));
            Assert.Equal("127.0.0.1:22000", s);
        }

        [Fact]
        public void DnsEndPointTypeConverter_roundtrips()
        {
            var conv = new DnsEndPointTypeConverter();
            var ep = (DnsEndPoint)conv.ConvertFrom(null, System.Globalization.CultureInfo.InvariantCulture, "example.com:8080");
            Assert.Equal("example.com", ep.Host);
            Assert.Equal(8080, ep.Port);
            var s = conv.ConvertTo(null, System.Globalization.CultureInfo.InvariantCulture, ep, typeof(string));
            Assert.Equal("example.com:8080", s);
        }

        [Fact]
        public void DnsEndPointTypeConverter_rejectsBadInput()
        {
            var conv = new DnsEndPointTypeConverter();
            Assert.Throws<FormatException>(() =>
                conv.ConvertFrom(null, System.Globalization.CultureInfo.InvariantCulture, "nocolon"));
            Assert.Throws<FormatException>(() =>
                conv.ConvertFrom(null, System.Globalization.CultureInfo.InvariantCulture, "host:notaport"));
        }

        // ---- JSON converters ----

        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("192.168.1.1")]
        public void IPAddressConverter_roundtrips(string address)
        {
            var ip = IPAddress.Parse(address);
            var json = JsonConvert.SerializeObject(ip, new IPAddressConverter());
            var back = JsonConvert.DeserializeObject<IPAddress>(json, new IPAddressConverter());
            Assert.Equal(ip, back);
        }

        [Fact]
        public void IPEndPointConverter_roundtrips()
        {
            var ep = new IPEndPoint(IPAddress.Loopback, 22000);
            var json = JsonConvert.SerializeObject(ep, new IPEndPointConverter());
            var back = JsonConvert.DeserializeObject<IPEndPoint>(json, new IPEndPointConverter());
            Assert.Equal(ep, back);
        }

        [Fact]
        public void DnsEndPointConverter_roundtrips()
        {
            var ep = new DnsEndPoint("example.com", 8080);
            var json = JsonConvert.SerializeObject(ep, new DnsEndPointConverter());
            var back = JsonConvert.DeserializeObject<DnsEndPoint>(json, new DnsEndPointConverter());
            Assert.Equal(ep.Host, back.Host);
            Assert.Equal(ep.Port, back.Port);
        }

        [Fact]
        public void TimeSpanConverter_roundtrips()
        {
            var ts = TimeSpan.FromMinutes(30);
            var json = JsonConvert.SerializeObject(ts, new OpenS4L.Common.Converters.Json.TimeSpanConverter());
            var back = JsonConvert.DeserializeObject<TimeSpan>(json, new OpenS4L.Common.Converters.Json.TimeSpanConverter());
            Assert.Equal(ts, back);
        }

        [Fact]
        public void VersionConverter_roundtrips()
        {
            var v = new Version(1, 2, 3);
            var json = JsonConvert.SerializeObject(v, new OpenS4L.Common.Converters.Json.VersionConverter());
            var back = JsonConvert.DeserializeObject<Version>(json, new OpenS4L.Common.Converters.Json.VersionConverter());
            Assert.Equal(v, back);
        }

        [Fact]
        public void PeerIdConverter_roundtrips()
        {
            var id = new PeerId(3, 4, 5);
            var json = JsonConvert.SerializeObject(id, new PeerIdConverter());
            var back = JsonConvert.DeserializeObject<PeerId>(json, new PeerIdConverter());
            Assert.Equal(id, back);
        }

        // ---- Extensions ----

        [Fact]
        public void TimeSpanExtensions_toHumanReadable()
        {
            Assert.Equal("", TimeSpan.Zero.ToHumanReadable());
            // Code appends "0 hour "/"0 minute " when a higher unit is present but this one is 0.
            Assert.Equal("1 day 0 hour ", TimeSpan.FromDays(1).ToHumanReadable());
            Assert.Equal("2 days 3 hours 0 minute ", (TimeSpan.FromDays(2) + TimeSpan.FromHours(3)).ToHumanReadable());
            Assert.Equal("5 minutes 6 seconds ", (TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(6)).ToHumanReadable());
        }

        [Fact]
        public void ObjectExtensions_toJson()
        {
            var obj = new { Name = "x", Value = 1 };
            Assert.Contains("\"Name\":\"x\"", obj.ToJson());
            Assert.Contains("\"Value\":1", obj.ToJson());
        }

        [Fact]
        public void ObjectExtensions_toJson_formatted()
        {
            var obj = new { A = 1 };
            var formatted = obj.ToJson(true);
            Assert.Contains("\n", formatted);
            var unformatted = obj.ToJson(false);
            Assert.DoesNotContain("\n", unformatted);
        }

        [Fact]
        public void DnsEndPointExtensions_toIPEndPoint()
        {
            var ep = new DnsEndPoint("localhost", 22000);
            var resolved = ep.ToIPEndPoint();
            Assert.Equal(22000, resolved.Port);
        }

        [Fact]
        public async System.Threading.Tasks.Task DnsEndPointExtensions_toIPEndPointAsync()
        {
            var ep = new DnsEndPoint("localhost", 22000);
            var resolved = await ep.ToIPEndPointAsync();
            Assert.Equal(22000, resolved.Port);
        }

        // ---- ServiceCollectionExtensions (light) ----

        [Fact]
        public void MessageWithGuid_holdsGuid()
        {
            var msg = new OpenS4L.Common.Messaging.MessageWithGuid { Guid = Guid.NewGuid() };
            Assert.NotEqual(Guid.Empty, msg.Guid);
        }

        private static string ReadAll(byte[] bytes) => Encoding.UTF8.GetString(bytes);
    }
}
