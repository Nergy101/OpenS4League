using System;
using System.Net;
using OpenS4L.Common.Configuration;

namespace OpenS4L.Server.Game
{
    public class AppOptions
    {
        public NetworkOptions Network { get; set; }
        public ServerListOptions ServerList { get; set; }
        public IPEndPoint RelayEndPoint { get; set; }
        public Version[] ClientVersions { get; set; }
        public DatabaseOptions Database { get; set; }
        public LoggerOptions Logging { get; set; }
        public TimeSpan SaveInterval { get; set; }

        // Write-behind cadence: how often dirty players are snapshotted+published to the Redis
        // queue, and how often the consumer drains the queue to Postgres. Stored as ms (like
        // SaveInterval) via the TimeSpan converter.
        public TimeSpan SavePublishInterval { get; set; } = TimeSpan.FromMilliseconds(5000);
        public TimeSpan SaveFlushInterval { get; set; } = TimeSpan.FromMilliseconds(2000);

        public GameOptions Game { get; set; }
    }
}
