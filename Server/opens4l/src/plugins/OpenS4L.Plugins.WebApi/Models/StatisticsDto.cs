namespace OpenS4L.Plugins.WebApi.Models
{
    public class StatisticsDto
    {
        public long Uptime { get; set; }
        public int PlayersOnline { get; set; }
        public int PeakPlayers { get; set; }

        public StatisticsDto()
        {
        }

        public StatisticsDto(long uptime, int playersOnline, int peakPlayers)
        {
            Uptime = uptime;
            PlayersOnline = playersOnline;
            PeakPlayers = peakPlayers;
        }

    }
}
