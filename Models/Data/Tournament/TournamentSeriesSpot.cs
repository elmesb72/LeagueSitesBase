public class TournamentSeriesSpot(char source, int seed, Team? team = null)
{
    public char Source { get; set; } = source;
    public int Seed { get; set; } = seed;
    public Team? Team { get; set; } = team;
}