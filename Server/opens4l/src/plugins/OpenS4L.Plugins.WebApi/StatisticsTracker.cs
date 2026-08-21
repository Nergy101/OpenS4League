using System.Threading;

namespace OpenS4L.Plugins.WebApi
{
    /// <summary>
    /// Tracks the peak (max) number of concurrently online players ever seen by this game server
    /// process. Updated from the /statistics endpoint so the admin console can show "Peak".
    /// </summary>
    public static class StatisticsTracker
    {
        private static int _peakPlayers;

        public static int PeakPlayers => Volatile.Read(ref _peakPlayers);

        /// <summary>Update the peak if <paramref name="players"/> is a new high-water mark.</summary>
        public static void Record(int players)
        {
            var current = Volatile.Read(ref _peakPlayers);
            while (players > current)
            {
                if (Interlocked.CompareExchange(ref _peakPlayers, players, current) == current)
                    return;
                current = Volatile.Read(ref _peakPlayers);
            }
        }
    }
}
