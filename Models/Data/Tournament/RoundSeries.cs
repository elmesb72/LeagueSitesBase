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
    public required string Format { get; set; }
    public required string HostOrder { get; set; }
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
    public Standings GetResults()
    {
        if (Results is null)
        {
            var games = Games.Select(g => g.Game).WhereNotNull();
            var playedGames = games.Where(g => g?.Status?.Name == "Played");
            Results = new Standings(playedGames);
        }
        return Results!;
    }

}