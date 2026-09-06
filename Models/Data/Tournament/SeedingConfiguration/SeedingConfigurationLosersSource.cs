using Microsoft.EntityFrameworkCore;

public class SeedingConfigurationLosersSource : ISeedingConfigurationSource
{
    string SourceType { get; }
    long SourceID { get; }
    int SourceRankStart { get; }
    int SourceRankEnd { get; }

    /*
        Examples:
        
        "BracketRound:15:1-4" means:
        - Return top four series losers from round ID 15

        "BracketRound:15:2-2" means:
        - Return second highest ranked series loser from round ID 15
    */
    readonly StandingsConfig? standingsConfig;

    public SeedingConfigurationLosersSource(string source, StandingsConfig? standingsConfig = null)
    {
        this.standingsConfig = standingsConfig;
        var sourceSplit = source.Split(':');
        SourceType = sourceSplit[0];
        SourceID = Convert.ToInt64(sourceSplit[1]);
        var sourceRankRange = sourceSplit[2].Split('-');
        SourceRankStart = Convert.ToInt32(sourceRankRange[0]);
        SourceRankEnd = Convert.ToInt32(sourceRankRange[1]);
    }

    public async Task<IEnumerable<Team>> GetTeamsAsync(LeagueSitesContext dbContext)
    {
        List<Team> teams = [];
        if (SourceType == "BracketRound")
        {
            var roundSeries = await dbContext.RoundSeries
                .Include(rs => rs.Round)
                    .ThenInclude(br => br.Bracket)
                .Include(rs => rs.Games)
                    .ThenInclude(sg => sg.Game)
                        .ThenInclude(g => g.HostTeam)
                .Include(rs => rs.Games)
                    .ThenInclude(sg => sg.Game)
                        .ThenInclude(g => g.VisitingTeam)
                .Include(rs => rs.Games)
                    .ThenInclude(sg => sg.Game)
                        .ThenInclude(g => g.Status)
                .Where(rs => rs.RoundID == SourceID)
                .ToListAsync();
            roundSeries.ForEach(rs => rs.CheckForWinnerAndLoser());
            var loserTeams = roundSeries.Where(rs => rs.Loser != null).Select(rs => rs.Loser!);

            // Since we no longer know what the original seedings were for the source round, we have to work it out again.
            // Then we can sort to keep the losers in the correct order.
            var originalSeedingConfiguration = roundSeries.First().Round!.Bracket!.SeedingConfiguration;
            var originalSeeds = await SeedingConfiguration.Parse(originalSeedingConfiguration, dbContext, standingsConfig);
            var loserSeeds = loserTeams.ToDictionary(l => originalSeeds.First(s => l == s.Value).Key, l => l);
            var sortedLoserTeams = loserSeeds.OrderBy(ls => ls.Key).Select(ls => ls.Value);
            teams.AddRange([.. sortedLoserTeams]);
        }
        return teams.Skip(SourceRankStart - 1).Take(SourceRankEnd - SourceRankStart + 1);
    }
}