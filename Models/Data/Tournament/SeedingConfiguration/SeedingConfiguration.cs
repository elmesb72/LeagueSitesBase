public static class SeedingConfiguration
{
    /*
        Examples:
        
        "1-8,Standings,Season:13:1-8" means:
        - Seeds 1-8 are based on the standings from Season ID 13 ranks 1-8
        
        "1-4,Losers,BracketRound:15;5-6,Standings,BracketRound:16:1-2" means:
        - Seeds 1-4 are based on the losers of BracketRound ID 15
        - Seeds 5-6 are based on the standings of BracketRound ID 16 ranks 1-2 (this would include winner and loser)
    */
    public static async Task<Dictionary<int, Team>> Parse(string config, LeagueSitesContext dbContext)
    {
        Dictionary<int, Team> seeds = [];
        foreach (var seedGroup in config.Split(';'))
        {
            // Parse string
            var seedGroupConfig = seedGroup.Split(',');
            var seedRankOutputRange = seedGroupConfig[0];
            var seedResult = seedGroupConfig[1];
            var seedSource = seedGroupConfig[2];

            // Get teams
            ISeedingConfigurationSource source = seedResult switch
            {
                "Standings" => new SeedingConfigurationStandingsSource(seedSource),
                "Losers" => new SeedingConfigurationLosersSource(seedSource),
                _ => throw new Exception($"Unknown seed result string {seedResult}."),
            };
            var teams = (await source.GetTeamsAsync(dbContext)).ToList();
            
            if (teams.Any())
            {
                // Assign to seeds
                var seedRankOutputRangeSplit = seedRankOutputRange.Split('-');
                var seedRankOutputStart = Convert.ToInt32(seedRankOutputRangeSplit[0]);
                var seedRankOutputEnd = Convert.ToInt32(seedRankOutputRangeSplit[1]);
                for (int i = seedRankOutputStart; i <= seedRankOutputEnd; i++)
                {
                    // A source can be partially available: a round robin part way through, or a
                    // round where only some series have finished. Fill the seeds we can and leave
                    // the rest unassigned rather than failing the whole tournament.
                    var sourceIndex = i - seedRankOutputStart;
                    if (sourceIndex >= teams.Count) break;
                    seeds[i] = teams[sourceIndex];
                }
            }
        }
        return seeds;
    }
}