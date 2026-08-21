using OpenS4L.Common.Configuration;

namespace OpenS4L.Server.Chat
{
    public class AppOptions
    {
        public NetworkOptions Network { get; set; }
        public ServerListOptions ServerList { get; set; }
        public DatabaseOptions Database { get; set; }
        public LoggerOptions Logging { get; set; }
        public MetricsOptions Metrics { get; set; }
    }

    /// <summary>HTTP metrics listener config (a tiny /statistics endpoint for the admin console).</summary>
    public class MetricsOptions
    {
        public string Listener { get; set; }
    }
}
