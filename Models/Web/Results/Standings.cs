/// <summary>
/// Insertion-ordered team standings: iterating the dictionary yields teams in
/// ranked order. Ranking follows the league's StandingsConfig — an ordered
/// list of comparators where each entry breaks ties left by the previous one,
/// with team name as the implicit final fallback. Head-to-head comparators
/// are evaluated against only the games among the still-tied teams, and when
/// a comparator splits a tied group, each resulting subgroup continues at the
/// next comparator with head-to-head metrics recomputed for the smaller
/// group.
/// </summary>
public class Standings : Dictionary<Team, TeamResultSet>
{
    /// <param name="games">The games that produce the records.</param>
    /// <param name="config">Ranking rules; null means the platform defaults.</param>
    /// <param name="teams">
    /// Teams that belong in the table whether or not they have played yet, such
    /// as the entrants seeded into a playoff pool. They get a 0-0-0 row and are
    /// ranked alongside everyone else, so membership is visible from the start
    /// instead of appearing one team at a time as games are scheduled.
    /// </param>
    public Standings(IEnumerable<Game> games, StandingsConfig? config = null, IEnumerable<Team>? teams = null)
    {
        config ??= new StandingsConfig();

        var teamGamesSet = new Dictionary<Team, List<Game>>();
        foreach (var t in teams ?? [])
        {
            teamGamesSet.TryAdd(t, []);
        }
        foreach (var g in games)
        {
            if (g.HostTeam is null || g.VisitingTeam is null)
            {
                throw new Exception("Game object missing Team data");
            }
            if (!teamGamesSet.ContainsKey(g.HostTeam))
            {
                teamGamesSet.Add(g.HostTeam, []);
            }
            if (!teamGamesSet.ContainsKey(g.VisitingTeam))
            {
                teamGamesSet.Add(g.VisitingTeam, []);
            }
            teamGamesSet[g.HostTeam].Add(g);
            teamGamesSet[g.VisitingTeam].Add(g);
        }

        var results = new Dictionary<Team, TeamResultSet>();
        foreach (var t in teamGamesSet.Keys)
        {
            results.Add(t, new TeamResultSet(t, teamGamesSet[t], config));
        }

        // The service layer guarantees a non-empty, known tiebreaker list,
        // but Standings is also constructed directly (tests, defaults), so
        // resolve defensively here too.
        var comparators = config.Tiebreakers
            .Select(StandingsComparators.Find)
            .WhereNotNull()
            .ToList();
        if (comparators.Count == 0)
        {
            comparators = [.. StandingsConfig.DefaultTiebreakers()
                .Select(StandingsComparators.Find)
                .WhereNotNull()];
        }

        foreach (var team in Rank([.. teamGamesSet.Keys], 0))
        {
            Add(team, results[team]);
        }

        IEnumerable<Team> Rank(List<Team> group, int comparatorIndex)
        {
            if (group.Count <= 1) return group;

            // All comparators exhausted: deterministic final fallback.
            if (comparatorIndex >= comparators.Count)
                return group.OrderBy(t => t.FullName);

            var comparator = comparators[comparatorIndex];
            Dictionary<Team, double> metric;
            if (comparator.GroupRestricted)
            {
                // Head-to-head scope: only games where both participants are
                // in the currently tied group.
                var members = new HashSet<Team>(group);
                metric = group.ToDictionary(
                    t => t,
                    t => comparator.Metric(new TeamResultSet(
                        t,
                        teamGamesSet[t].Where(g =>
                            members.Contains(g.HostTeam!) && members.Contains(g.VisitingTeam!)),
                        config)));
            }
            else
            {
                metric = group.ToDictionary(t => t, t => comparator.Metric(results[t]));
            }

            // Order subgroups by metric; any subgroup still holding more than
            // one team moves on to the next comparator, with head-to-head
            // metrics recomputed against the smaller group.
            return group
                .GroupBy(t => metric[t])
                .OrderByDescending(g => g.Key)
                .SelectMany(g => Rank([.. g], comparatorIndex + 1));
        }
    }

    public void CalculateStreaks()
    {
        foreach (var t in this)
        {
            t.Value.CalculateStreak();
        }
    }
}
