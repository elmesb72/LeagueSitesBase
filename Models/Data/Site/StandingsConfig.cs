/// <summary>
/// Standings rules, stored per season as a JSON blob in Season.StandingsJson
/// so that rule changes between seasons never rewrite how historical seasons
/// are ranked (standings, records, and playoff seeding all resolve against
/// the season that owns the games).
///
/// The defaults are the platform's canonical rules: 2/1/0 points, 7-0
/// forfeits, and ranking by points, wins, then head-to-head wins and
/// head-to-head run differential among tied teams, with team name as the
/// implicit final fallback. (Before 2026-09 the code ranked by overall run
/// differential instead of head-to-head — that was wrong for the leagues on
/// this platform and was corrected retroactively by migration 0003.)
/// Absent or unparsable stored JSON must resolve to these defaults — see
/// StandingsConfigService.
/// </summary>
public class StandingsConfig
{
    public int WinsValue { get; set; } = 2;
    public int TiesValue { get; set; } = 1;
    public int LossesValue { get; set; } = 0;

    /// <summary>Score recorded for the non-forfeiting team in a forfeited game.</summary>
    public int ForfeitWinnerScore { get; set; } = 7;
    /// <summary>Score recorded for the forfeiting team in a forfeited game.</summary>
    public int ForfeitLoserScore { get; set; } = 0;

    /// <summary>
    /// Ordered comparator names from StandingsComparators.All. The first
    /// entry is the primary ranking; each later entry breaks ties remaining
    /// after the ones before it. Team full name is always applied as an
    /// implicit final fallback so the order is a deterministic total order.
    /// </summary>
    public List<string> Tiebreakers { get; set; } = DefaultTiebreakers();

    public static List<string> DefaultTiebreakers() =>
        ["Points", "Wins", "HeadToHeadWins", "HeadToHeadRunDifferential"];
}
