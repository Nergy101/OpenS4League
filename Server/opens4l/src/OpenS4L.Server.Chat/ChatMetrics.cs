using System.Threading;
using Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace OpenS4L.Server.Chat
{
    /// <summary>
    /// Thread-safe chat metrics counters. Incremented by ChatHandler as messages are relayed and
    /// read by <see cref="ChatMetricsService"/> for the admin console.
    /// </summary>
    public static class ChatMetrics
    {
        private static long _messagesSent;
        private static long _whispersSent;

        public static long MessagesSent => Interlocked.Read(ref _messagesSent);
        public static long WhispersSent => Interlocked.Read(ref _whispersSent);

        public static void IncrementMessageSent() => Interlocked.Increment(ref _messagesSent);
        public static void IncrementWhisperSent() => Interlocked.Increment(ref _whispersSent);
    }
}
