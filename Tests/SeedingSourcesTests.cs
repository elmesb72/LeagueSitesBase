using FluentAssertions;

namespace LeagueSitesBackend.Tests;

/// <summary>
/// The seeding sources tell the management UI how many teams each place can
/// supply; the forms refuse a rule that asks for more. A pool's count has to be
/// structural, like a bracket round's, or a B-side final cannot be set up until
/// the round that fills the pool has actually been played.
/// </summary>
public class SeedingSourcesTests
{
    static readonly Team[] Teams = [.. Enumerable.Range(1, 6)
        .Select(i => TestDataHelper.MakeTeam(i, $"Team{i}", $"T{i}"))];

    static Tournament TournamentWithPool(TournamentRoundRobin pool) => new()
    {
        ID = 1,
        SeasonID = 2,
        RoundRobins = [pool],
    };

    static SeedingSourceOptionDto PoolSource(Tournament tournament) =>
        TournamentService.BuildSeedingSources(tournament, regularSeason: null, regularSeasonTeamCount: 0)
            .Single(s => s.SourceType == SeedGroup.SourceRoundRobin);

    [Fact]
    public void A_pool_nobody_has_qualified_for_yet_still_offers_its_configured_seats()
    {
        // Four quarter-final losers, none decided yet: no standings, no resolved seeds.
        var pool = new TournamentRoundRobin
        {
            ID = 7,
            Name = "B Side",
            Historical = false,
            SeedingConfiguration = "1-4,Losers,BracketRound:15:1-4",
            Seeds = [],
            Standings = null,
        };

        var source = PoolSource(TournamentWithPool(pool));

        source.Label.Should().Be("B Side final standings");
        source.AvailableTeams.Should().Be(4);
    }

    [Fact]
    public void Two_rules_add_up()
    {
        var pool = new TournamentRoundRobin
        {
            ID = 7,
            Name = "B Side",
            Historical = false,
            SeedingConfiguration = "1-2,Losers,BracketRound:15:1-2;3-4,Standings,Season:13:9-10",
            Seeds = [],
        };

        PoolSource(TournamentWithPool(pool)).AvailableTeams.Should().Be(4);
    }

    [Fact]
    public void Resolved_teams_beyond_the_configuration_still_count()
    {
        // Legacy pools have no stored configuration and fall back to the regular season.
        var pool = new TournamentRoundRobin
        {
            ID = 7,
            Name = "Consolation",
            Historical = false,
            SeedingConfiguration = null,
            Seeds = Teams.Select((t, i) => (Seed: i + 1, Team: t)).ToDictionary(x => x.Seed, x => x.Team),
        };

        PoolSource(TournamentWithPool(pool)).AvailableTeams.Should().Be(6);
    }
}
