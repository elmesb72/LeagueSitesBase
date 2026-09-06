/// <summary>
/// One orderable standings metric. Global comparators read a team's season
/// result set; group-restricted (head-to-head) comparators are evaluated
/// against a result set rebuilt from only the games among the tied teams.
/// Metrics are oriented so that larger is always better.
/// </summary>
public sealed record StandingsComparator(
    string Name,
    string Description,
    bool GroupRestricted,
    Func<TeamResultSet, double> Metric);

/// <summary>
/// The registry of comparators a league can rank standings by. Serialized to
/// the Webmaster UI (names + descriptions), referenced by name from
/// StandingsConfig.Tiebreakers, and resolved by the Standings ranking
/// algorithm. Adding a comparator here makes it available everywhere.
/// </summary>
public static class StandingsComparators
{
    public static readonly IReadOnlyList<StandingsComparator> All =
    [
        new("Points",
            "Points, using the configured win/tie/loss values",
            false, r => r.Points),
        new("Wins",
            "Most wins",
            false, r => r.Wins),
        new("WinPercentage",
            "Win percentage: wins plus half of ties, divided by games played",
            false, r => r.GamesPlayed == 0 ? 0 : (r.Wins + r.Ties / 2.0) / r.GamesPlayed),
        new("RunDifferential",
            "Run differential: runs scored minus runs allowed",
            false, r => r.RunDifferential),
        new("RunsScored",
            "Most runs scored",
            false, r => r.RunsScored),
        new("FewestRunsAllowed",
            "Fewest runs allowed",
            false, r => -r.RunsAllowed),
        new("HeadToHeadPoints",
            "Points in games between the tied teams, using the configured values",
            true, r => r.Points),
        new("HeadToHeadWins",
            "Wins in games between the tied teams",
            true, r => r.Wins),
        new("HeadToHeadRunDifferential",
            "Run differential in games between the tied teams",
            true, r => r.RunDifferential),
    ];

    public static StandingsComparator? Find(string name) =>
        All.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}
