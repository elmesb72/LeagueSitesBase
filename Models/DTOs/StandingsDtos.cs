/// <summary>
/// Standings DTOs are manually defined since Standings is a computed
/// Dictionary&lt;Team, TeamResultSet&gt; rather than an EF entity.
/// </summary>
public record StandingsEntryDto(
    TeamSummaryDto Team,
    int GamesPlayed,
    int Wins,
    int Losses,
    int Ties,
    int Points,
    int RunsScored,
    int RunsAllowed,
    int RunDifferential,
    string Record,
    string HomeRecord,
    string AwayRecord,
    string? Streak);

public static class StandingsDtoExtensions
{
    public static List<StandingsEntryDto> ToDto(this Standings standings)
    {
        return standings.Select(s =>
        {
            var home = s.Value.Results.Where(r => r.WasHome);
            var away = s.Value.Results.Where(r => !r.WasHome);

            return new StandingsEntryDto(
                new TeamSummaryDto(s.Key),
                s.Value.GamesPlayed,
                s.Value.Wins,
                s.Value.Losses,
                s.Value.Ties,
                s.Value.Points,
                s.Value.RunsScored,
                s.Value.RunsAllowed,
                s.Value.RunDifferential,
                s.Value.ToString(),
                $"{home.Count(r => r.Result == GameResult.Win)}-{home.Count(r => r.Result == GameResult.Loss)}-{home.Count(r => r.Result == GameResult.Tie)}",
                $"{away.Count(r => r.Result == GameResult.Win)}-{away.Count(r => r.Result == GameResult.Loss)}-{away.Count(r => r.Result == GameResult.Tie)}",
                s.Value.Streak);
        }).ToList();
    }
}
