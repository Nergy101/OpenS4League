using OpenS4L.Common.Configuration;

namespace OpenS4L.Server.Relay
{
    public class AppOptions
    {
        public NetworkOptions Network { get; set; }
        public DatabaseOptions Database { get; set; }
        public LoggerOptions Logging { get; set; }
    }
}
