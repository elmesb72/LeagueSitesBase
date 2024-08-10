public class Standings : Dictionary<Team, TeamResultSet>
{
    public Standings(IEnumerable<Game> games)
    {
        var teamGamesSet = new Dictionary<Team, List<Game>>();
        foreach (var g in games)
        {
            if (g.HostTeam is null || g.VisitingTeam is null) {
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
            results.Add(t, new TeamResultSet(t, teamGamesSet[t]));
        }
        foreach (var row in results.OrderByDescending(r => r.Value.Points)
                    .ThenByDescending(r => r.Value.Wins)
                    .ThenByDescending(r => r.Value.RunDifferential)
                .ThenBy(r => r.Key.FullName))
        {
            this.Add(row.Key, row.Value);
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