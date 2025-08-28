using Microsoft.EntityFrameworkCore;

public class SeedingConfigurationStandingsSource : ISeedingConfigurationSource
{
    string SourceType { get; }
    long SourceID { get; }
    int SourceRankStart { get; }
    int SourceRankEnd { get; }

    /*
        Examples:
        
        "Season:13:1-8" means:
        - Return ranks 1-8 from Season ID 13
        
        "BracketRound:16:1-2" means:
        - Return ranks 1-2 from BracketRound ID 16
    */
    public SeedingConfigurationStandingsSource(string source)
    {
        var sourceSplit = source.Split(':');
        SourceType = sourceSplit[0];
        SourceID = Convert.ToInt64(sourceSplit[1]);
        var sourceRankRange = sourceSplit[2].Split('-');
        SourceRankStart = Convert.ToInt32(sourceRankRange[0]);
        SourceRankEnd = Convert.ToInt32(sourceRankRange[1]);
    }

    public async Task<IEnumerable<Team>> GetTeamsAsync(LeagueSitesContext dbContext)
    {
        List<Game> games = [];
        if (SourceType == "Season")
        {
            games.AddRange(await dbContext.Games
                .Include(g => g.HostTeam)
                .Include(g => g.VisitingTeam)
                .Include(g => g.Status)
                .Where(g => g.SeasonID == SourceID)
                .ToListAsync());
        }
        else if (SourceType == "BracketRound")
        {
            var roundSeries = await dbContext.RoundSeries
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
            games.AddRange(roundSeries.SelectMany(sg => sg.Games).Select(sg => sg.Game!));
        }
        else if (SourceType == "TournamentRoundRobin")
        {
            var tournamentRoundRobin = await dbContext.TournamentRoundRobins
                .Include(rs => rs.Games)
                    .ThenInclude(sg => sg.Game)
                        .ThenInclude(g => g.HostTeam)
                .Include(rs => rs.Games)
                    .ThenInclude(sg => sg.Game)
                        .ThenInclude(g => g.VisitingTeam)
                .Include(rs => rs.Games)
                    .ThenInclude(sg => sg.Game)
                        .ThenInclude(g => g.Status)
                .Where(rs => rs.ID == SourceID)
                .ToListAsync();
            games.AddRange(tournamentRoundRobin.SelectMany(trr => trr.Games).Select(trrg => trrg.Game!));
        }
        return new Standings(games).Keys.Skip(SourceRankStart - 1).Take(SourceRankEnd - SourceRankStart + 1);
    }
}