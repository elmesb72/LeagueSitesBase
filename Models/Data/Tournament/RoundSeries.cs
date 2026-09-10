using System.ComponentModel.DataAnnotations.Schema;

public partial class RoundSeries
{
    public RoundSeries()
    {
        Games = [];
    }

    public long ID { get; set; }
    public long RoundID { get; set; }
    public long Number { get; set; }
    public required string Format { get; set; } // "Best of" // TODO add "Aggregate"
    public required string HostOrder { get; set; } // e.g. 121 is higher seed home-away-home. 1122121 would represent NHL's best of 7 format.
    public required string Matchup { get; set; } // '#x' for initial seeding: x refers to input rank, 'wx' or 'lx' for fixed seed ('winner of x'/'loser of x'): x refers to series Number; 'rx' for re-seed: x refers to remaining rank. Example "#1-#8", "w5-w6", "r1-r4"

    [NotMapped]
    public (TournamentSeriesSpot, TournamentSeriesSpot) Spots { get; set; }
    [NotMapped]
    public Team? Winner { get; set; }
    [NotMapped]
    public Team? Loser { get; set; }

    public virtual BracketRound? Round { get; set; }
    public ICollection<SeriesGame> Games { get; set; }

    public void CheckForWinnerAndLoser()
    {
        if (Games.Count == 0 || Games.All(g => g.Game is null))
        {
            return;
        }

        var seriesLength = HostOrder.Length;
        var toWin = Convert.ToInt32(Math.Ceiling(seriesLength / 2d));
        var results = GetResults();
        if (Format == "Best of")
        {
            Winner = results.FirstOrDefault(t => t.Value.Wins >= toWin).Key;
            Loser = Winner is not null ? results.Last().Key : null;
        }
        else if (Format == "Aggregate" && results.First().Value.GamesPlayed == seriesLength)
        {
            Winner = results.OrderByDescending(t => t.Value.RunDifferential).First().Key;
            Loser = results.OrderByDescending(t => t.Value.RunDifferential).Last().Key;
        }
    }

    Standings? Results { get; set; }

    // A game counts toward a series once it has a result: played, or awarded by
    // forfeit. Forfeits were excluded here, which left a series won on one with
    // no Winner at all — and since the next round's w/l/r spots are resolved
    // from series winners, a single forfeit stalled the rest of the bracket.
    // Upcoming/Cancelled/Postponed/Deleted are still ignored, including when
    // they carry a stale score; TeamResultSet substitutes the league's
    // configured forfeit score for the two forfeit statuses.
    static readonly string[] DecidedStatuses = ["Played", "Forfeit (Home)", "Forfeit (Away)"];

    // Uses default StandingsConfig deliberately: series are decided by game
    // WINS per team (and run differential for Aggregate), read directly off
    // the result sets — no configured ranking rule applies to a series. The
    // Loser = results.Last() logic in CheckForWinnerAndLoser also relies on
    // the default order, where the series winner always sorts first. See the
    // configurable-standings-rules spec, Requirement 4.2.
    public Standings GetResults()
    {
        if (Results is null)
        {
            var games = Games.Select(g => g.Game).WhereNotNull();
            var decidedGames = games.Where(g => DecidedStatuses.Contains(g?.Status?.Name));
            Results = new Standings(decidedGames);
        }
        return Results!;
    }

}