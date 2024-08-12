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

    public void Populate(List<Game> playoffGames, Standings seeds)
    {
        // Map game objects to bracket game objects
        foreach (var bracket in Brackets)
        {
            foreach (var round in bracket.Rounds)
            {
                foreach (var series in round.Series)
                {
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

                    // Add Team info to spot details where based on initial seed
                    if (series.Spots.Item1.Source == '#') series.Spots.Item1.Team = seeds.Keys.ElementAt(series.Spots.Item1.Seed - 1);
                    if (series.Spots.Item2.Source == '#') series.Spots.Item2.Team = seeds.Keys.ElementAt(series.Spots.Item2.Seed - 1);
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
                    if (series.Spots.Item1.Team is null || series.Spots.Item2.Team is null) {
                        continue;
                    }
                    if (seeds.Keys.ToList().IndexOf(series.Spots.Item2.Team) < seeds.Keys.ToList().IndexOf(series.Spots.Item1.Team)) // if Team 2 initial rank is higher (aka lower index) than team 1, swap places
                    {
                        series.Spots = (series.Spots.Item2, series.Spots.Item1);
                    }
                    if (series.Spots.Item1.Source == 'r' && remainingTeams.Count > series.Spots.Item1.Seed - 1) series.Spots.Item1.Team = remainingTeams.ElementAt(series.Spots.Item1.Seed - 1).Value;
                    if (series.Spots.Item2.Source == 'r' && remainingTeams.Count > series.Spots.Item2.Seed - 1) series.Spots.Item2.Team = remainingTeams.ElementAt(series.Spots.Item2.Seed - 1).Value;
                }

                var winners = round.Series.Where(s => s.Winner is not null).Select(s => s.Winner);
                remainingTeams = remainingTeams.Where(t => winners.Any(w => w?.ID == t.Value?.ID)).ToDictionary(t => t.Key, t => t.Value);
            }
        }

        // Create standings for round robins
        foreach (var roundrobin in RoundRobins)
        {
            roundrobin.Standings = new Standings(roundrobin.Games.Select(g => g.Game ?? new Game()));
        }
    }

}
