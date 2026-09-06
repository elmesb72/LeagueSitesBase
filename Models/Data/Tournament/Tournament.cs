using Microsoft.EntityFrameworkCore;

public partial class Tournament
{
    public Tournament()
    {
        Brackets = [];
        RoundRobins = [];
    }

    public long ID { get; set; }
    public long SeasonID { get; set; }

    public virtual Season? Season { get; set; }
    public ICollection<TournamentBracket> Brackets { get; set; }
    public ICollection<TournamentRoundRobin> RoundRobins { get; set; }

    /// <param name="standingsConfig">
    /// League standings rules, used wherever seeding or pool order depends on
    /// a standings ranking. Null means default rules.
    /// </param>
    public async Task Populate(List<Game> playoffGames, LeagueSitesContext dbContext, StandingsConfig? standingsConfig = null)
    {
        // Map game objects to bracket game objects
        foreach (var bracket in Brackets)
        {
            if (!string.IsNullOrEmpty(bracket.SeedingConfiguration))
            {
                bracket.Seeds = await SeedingConfiguration.Parse(bracket.SeedingConfiguration, dbContext, standingsConfig);
            }
            else
            {
                // Regular season standings
                var regularSeason = await dbContext.Seasons.FirstAsync(s => s.Year == Season!.Year && s.Subseason == "Regular Season");
                var standings = new Standings(await dbContext.Games.Where(g => g.SeasonID == regularSeason.ID).ToListAsync(), standingsConfig);
                bracket.Seeds = await SeedingConfiguration.Parse($"1-{standings.Count},Standings,Season:{regularSeason.ID}:1-{standings.Count}", dbContext, standingsConfig);
            }
            
            foreach (var round in bracket.Rounds)
            {
                foreach (var series in round.Series)
                {
                    if (!bracket.Seeds.Any()) continue;

                    // Parse spot details
                    var spots = series.Matchup.Split("-");
                    series.Spots = (new TournamentSeriesSpot(spots[0][0], int.Parse(spots[0][1..])), new TournamentSeriesSpot(spots[1][0], int.Parse(spots[1][1..])));

                    // Associate Game object (if exists)
                    foreach (var game in series.Games)
                    {
                        game.Game = playoffGames.FirstOrDefault(g => game.GameID == g.ID);
                    }

                    // Set Winner property on series if series is finished.
                    series.CheckForWinnerAndLoser();

                    // Add Team info to spot details where based on initial seed.
                    // A seed can legitimately be unfilled if its source has not finished yet.
                    if (series.Spots.Item1.Source == '#' && bracket.Seeds.TryGetValue(series.Spots.Item1.Seed, out var seed1Team))
                        series.Spots.Item1.Team = seed1Team;
                    if (series.Spots.Item2.Source == '#' && bracket.Seeds.TryGetValue(series.Spots.Item2.Seed, out var seed2Team))
                        series.Spots.Item2.Team = seed2Team;
                }
            }
        }

        // Map game objects to round robin game objects
        foreach (var roundrobin in RoundRobins)
        {
            // Associate Game object (if exists)
            foreach (var game in roundrobin.Games)
            {
                game.Game = playoffGames.FirstOrDefault(g => game.GameID == g.ID);
            }
        }

        // Check for winners and losers
        foreach (var bracket in Brackets)
        {
            foreach (var round in bracket.Rounds)
            {
                foreach (var series in round.Series)
                {
                    series.CheckForWinnerAndLoser();
                }
            }
        }

        // Set spots that are determined based on winners & losers, and re-seedings
        var allSeries = Brackets.SelectMany(b => b.Rounds).SelectMany(r => r.Series);
        foreach (var bracket in Brackets)
        {
            if (!bracket.Seeds.Any()) continue;
            
            var seriesSpots = bracket.Rounds.First().Series.Select(s => s.Spots);
            var remainingTeams = seriesSpots.ToDictionary(s => s.Item1.Seed, s => s.Item1.Team).Union(
                seriesSpots.ToDictionary(s => s.Item2.Seed, s => s.Item2.Team)
                ).OrderBy(t => t.Key)
                .ToDictionary(t => t.Key, t => t.Value);
            bracket.Rounds = bracket.Rounds.OrderByDescending(r => r.Series.Count).ToList(); // Ensure rounds are in order

            foreach (var round in bracket.Rounds)
            {
                foreach (var series in round.Series)
                {
                    if (series.Spots.Item1.Source == 'w') series.Spots.Item1.Team = allSeries?.FirstOrDefault(s => s.Number == series.Spots.Item1.Seed)?.Winner;
                    if (series.Spots.Item1.Source == 'l') series.Spots.Item1.Team = allSeries?.FirstOrDefault(s => s.Number == series.Spots.Item1.Seed)?.Loser;
                    if (series.Spots.Item2.Source == 'w') series.Spots.Item2.Team = allSeries?.FirstOrDefault(s => s.Number == series.Spots.Item2.Seed)?.Winner;
                    if (series.Spots.Item2.Source == 'l') series.Spots.Item2.Team = allSeries?.FirstOrDefault(s => s.Number == series.Spots.Item2.Seed)?.Loser;

                    // If teams are set and team 2 initial rank is higher (aka lower index) than team 1, swap places.
                    // Teams that entered from another bracket may not appear in this bracket's seeds, in
                    // which case there is no seed order to compare and the spots stay as they are.
                    if (series.Spots.Item1.Team != null && series.Spots.Item2.Team != null)
                    {
                        var team1SeedEntry = bracket.Seeds.FirstOrDefault(s => s.Value == series.Spots.Item1.Team!);
                        var team2SeedEntry = bracket.Seeds.FirstOrDefault(s => s.Value == series.Spots.Item2.Team!);
                        if (team1SeedEntry.Value != null && team2SeedEntry.Value != null
                            && team2SeedEntry.Key < team1SeedEntry.Key)
                        {
                            series.Spots = (series.Spots.Item2, series.Spots.Item1);
                        }
                    }

                    if (series.Spots.Item1.Source == 'r' && remainingTeams.Count > series.Spots.Item1.Seed - 1) series.Spots.Item1.Team = remainingTeams.ElementAt(series.Spots.Item1.Seed - 1).Value;
                    if (series.Spots.Item2.Source == 'r' && remainingTeams.Count > series.Spots.Item2.Seed - 1) series.Spots.Item2.Team = remainingTeams.ElementAt(series.Spots.Item2.Seed - 1).Value;
                }

                var winners = round.Series.Where(s => s.Winner != null).Select(s => s.Winner);
                remainingTeams = remainingTeams.Where(t => winners.Any(w => w?.ID == t.Value?.ID)).ToDictionary(t => t.Key, t => t.Value);
            }
        }

        // Create standings for round robins (pool order follows league rules)
        foreach (var roundrobin in RoundRobins)
        {
            roundrobin.Standings = new Standings(roundrobin.Games.Select(g => g.Game ?? new Game()), standingsConfig);

            if (!string.IsNullOrEmpty(roundrobin.SeedingConfiguration))
            {
                roundrobin.Seeds = await SeedingConfiguration.Parse(roundrobin.SeedingConfiguration, dbContext, standingsConfig);
            }
            else
            {
                // Regular season standings
                var regularSeason = await dbContext.Seasons.FirstAsync(s => s.Year == Season!.Year && s.Subseason == "Regular Season");
                var standings = new Standings(await dbContext.Games.Where(g => g.SeasonID == regularSeason.ID).ToListAsync(), standingsConfig);
                roundrobin.Seeds = await SeedingConfiguration.Parse($"1-{standings.Count},Standings,Season:{regularSeason.ID}:1-{standings.Count}", dbContext, standingsConfig);
            }
        }
    }

}
