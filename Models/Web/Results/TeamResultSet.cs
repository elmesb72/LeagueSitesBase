public class TeamResultSet
{
    public readonly List<TeamResult> Results = [];
    public int GamesPlayed => Wins + Losses + Ties;
    public readonly int Wins;
    public readonly int Losses;
    public readonly int Ties;
    public readonly int Points;
    public readonly int RunsScored;
    public readonly int RunsAllowed;
    public int RunDifferential => RunsScored - RunsAllowed;
    public string? Streak { get; private set; }

    public TeamResultSet(Team team, IEnumerable<Game> games, int winsValue = 2, int tiesValue = 1, int lossesValue = 0)
    {
        Points = 0;

        foreach (var g in games)
        {
            if (g?.Status?.Name == "Forfeit (Home)")
            {
                g.ScoreVisitor = 7;
                g.ScoreHost = 0;
            }
            else if (g?.Status?.Name == "Forfeit (Away)")
            {
                g.ScoreHost = 7;
                g.ScoreVisitor = 0;
            }
            else if (g?.ScoreHost is null || g?.ScoreVisitor is null)
            {
                continue;
            }
            Results.Add(new TeamResult(g, team));
        }
        Wins = Results.Count(r => r.Result == GameResult.Win);
        Losses = Results.Count(r => r.Result == GameResult.Loss);
        Ties = Results.Count(r => r.Result == GameResult.Tie);
        Points = (Wins * winsValue) + (Ties * tiesValue) + (Losses * lossesValue);
        RunsScored = Results.Sum(r => r.RunsScored);
        RunsAllowed = Results.Sum(r => r.RunsAllowed);
    }

    public void CalculateStreak()
    {
        var sortedResults = Results.OrderByDescending(g => g.Game.Date);
        if (!sortedResults.Any())
        { // Before playing any games, nobody has a streak
            Streak = "-";
            return;
        }
        var streak = sortedResults.First()?.Result ?? GameResult.Tie;
        int length = 0;
        foreach (var r in sortedResults)
        {
            if (r.Result == streak)
            {
                length++;
            }
            else
            {
                break;
            }
        }
        Streak = streak.ToString()[..1] + length;
    }

    /// TO DO: League scoring set via configuration and passed in

    public override string ToString() => "(" + Wins + "-" + Losses + "-" + Ties + ")";
}