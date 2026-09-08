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
        // Standings rules are per-season: rank the source's games under the
        // rules of the season those games belong to, so historical seeding
        // never changes when a later season adopts different rules.
        string? standingsJson = null;

        List<Game> games = [];
        if (SourceType == "Season")
        {
            games.AddRange(await dbContext.Games
                .Include(g => g.HostTeam)
                .Include(g => g.VisitingTeam)
                .Include(g => g.Status)
                .Where(g => g.SeasonID == SourceID)
                .ToListAsync());
            standingsJson = await dbContext.Seasons
                .AsNoTracking()
                .Where(s => s.ID == SourceID)
                .Select(s => s.StandingsJson)
                .FirstOrDefaultAsync();
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
            standingsJson = await dbContext.RoundSeries
                .AsNoTracking()
                .Where(rs => rs.RoundID == SourceID)
                .Select(rs => rs.Round!.Bracket!.Tournament!.Season!.StandingsJson)
                .FirstOrDefaultAsync();
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
            standingsJson = await dbContext.TournamentRoundRobins
                .AsNoTracking()
                .Where(rr => rr.ID == SourceID)
                .Select(rr => rr.Tournament!.Season!.StandingsJson)
                .FirstOrDefaultAsync();
        }

        // Rank with the owning season's rules so a team seeded "3rd in the
        // standings" is the same team that season's standings page shows 3rd.
        var config = StandingsConfigService.Parse(standingsJson);
        return new Standings(games, config).Keys.Skip(SourceRankStart - 1).Take(SourceRankEnd - SourceRankStart + 1);
    }
}