public partial class TournamentBracket
{
    public TournamentBracket()
    {
        Rounds = [];
    }

    public long ID { get; set; }
    public required string Name { get; set; }
    public long TournamentID { get; set; }
    public required string HigherSeedSource { get; set; } // "Standings", "Fixed"
    public required string Format { get; set; } // "Fixed", "Re-seed"
    
    public virtual Tournament? Tournament { get; set; }
    public ICollection<BracketRound> Rounds { get; set; }

    public Team GetWinner()
    {
        var series = Rounds.SelectMany(r => r.Series);
        var winners = series.Select(s => s.Winner).WhereNotNull();
        var distinctWinners = winners.GroupBy(w => w).ToDictionary(dw => dw.Key, wg => wg.Count());
        var teamWithMostWins = distinctWinners.OrderByDescending(wg => wg.Value).First().Key;
        return teamWithMostWins;
    }

}
