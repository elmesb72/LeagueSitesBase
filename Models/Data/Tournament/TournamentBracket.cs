using System.ComponentModel.DataAnnotations.Schema;

public partial class TournamentBracket
{
    public TournamentBracket()
    {
        Rounds = [];
    }

    public long ID { get; set; }
    public required string Name { get; set; }
    public long TournamentID { get; set; }
    public string? SeedingConfiguration { get; set; }
    public required string Format { get; set; } // "Fixed", "Re-seed"
    public required bool Historical { get; set; } // if true, the winner of this bracket is shown on the league History page
    
    public virtual Tournament? Tournament { get; set; }
    public ICollection<BracketRound> Rounds { get; set; }

    [NotMapped]
    public Dictionary<int, Team> Seeds { get; set; }

    /// <summary>
    /// True once every series in the bracket has a winner — the same test the
    /// History page applies before naming a champion (Year.PlayoffsAreComplete).
    /// Check this before calling GetWinner: with only the opening round played,
    /// every first-round winner has exactly one series win, so GetWinner would
    /// hand back whichever of them happens to be first and call it the champion.
    /// </summary>
    public bool IsDecided()
    {
        var series = Rounds.SelectMany(r => r.Series).ToList();
        return series.Count > 0 && series.All(s => s.Winner is not null);
    }

    /// <summary>
    /// The team with the most series wins. Only meaningful when IsDecided() —
    /// in a finished single-elimination bracket that is the champion.
    /// </summary>
    public Team GetWinner()
    {
        var series = Rounds.SelectMany(r => r.Series);
        var winners = series.Select(s => s.Winner).WhereNotNull();
        var distinctWinners = winners.GroupBy(w => w).ToDictionary(dw => dw.Key, wg => wg.Count());
        var teamWithMostWins = distinctWinners.OrderByDescending(wg => wg.Value).First().Key;
        return teamWithMostWins;
    }

}
