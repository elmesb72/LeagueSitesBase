public class TournamentSeriesSpot
{
    public char Source { get; set; } // "#x" refers to initial rank x, "w#" refers to winner of series #, "t#" refers to top remaining #.
    public int Seed { get; set; }
    public Team? Team { get; set; }

    public TournamentSeriesSpot(char source, int seed, Team? team = null)
    {
        Source = source;
        Seed = seed;
        Team = team;
    }
}