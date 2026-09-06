/// <summary>
/// Per-tenant standings rules, stored as a JSON blob in
/// SiteConfig.StandingsJson (the same pattern as HomeJson/HistoryJson).
///
/// The defaults reproduce the historic hardcoded behavior exactly:
/// 2/1/0 points, 7-0 forfeits, and ranking by points, then wins, then run
/// differential, with team name as the implicit final fallback. Absent or
/// unparsable stored JSON must resolve to these defaults — see
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

    public static List<string> DefaultTiebreakers() => ["Points", "Wins", "RunDifferential"];
}
