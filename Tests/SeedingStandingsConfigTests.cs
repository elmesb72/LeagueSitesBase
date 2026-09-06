using FluentAssertions;
using Moq;
using Moq.EntityFrameworkCore;

namespace LeagueSitesBackend.Tests;

/// <summary>
/// The motivating scenario for configurable standings rules: playoff seeding
/// must rank teams with the league's configured tiebreakers, so the team the
/// standings page shows in a rank is the team seeded at that rank.
/// </summary>
public class SeedingStandingsConfigTests
{
    static readonly Team TeamA = TestDataHelper.MakeTeam(1, "Alphas", "ALP");
    static readonly Team TeamB = TestDataHelper.MakeTeam(2, "Betas", "BET");
    static readonly Team TeamC = TestDataHelper.MakeTeam(3, "Gammas", "GAM");
    static readonly Team TeamD = TestDataHelper.MakeTeam(4, "Deltas", "DEL");

    const long SeasonID = 13;

    /// <summary>
    /// D sweeps; C loses out. A and B are tied on points and wins — A has the
    /// far better overall run differential, but B won the head-to-head game.
    /// </summary>
    static Mock<LeagueSitesContext> MockSeasonGames()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamC, "Played", scoreHost: 10, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamB, TeamA, "Played", scoreHost: 2, scoreVisitor: 1),
            TestDataHelper.MakeGame(TeamD, TeamB, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamD, TeamC, "Played", scoreHost: 5, scoreVisitor: 0),
        };
        games.ForEach(g => g.SeasonID = SeasonID);

        var dbMock = new Mock<LeagueSitesContext>();
        dbMock.Setup(x => x.Games).ReturnsDbSet(games);
        return dbMock;
    }

    [Fact]
    public async Task StandingsSource_DefaultRules_RanksByRunDifferential()
    {
        var source = new SeedingConfigurationStandingsSource($"Season:{SeasonID}:1-4");

        var teams = (await source.GetTeamsAsync(MockSeasonGames().Object)).ToList();

        teams.Should().Equal(TeamD, TeamA, TeamB, TeamC);
    }

    [Fact]
    public async Task StandingsSource_HeadToHeadRules_RanksMeetingWinnerAhead()
    {
        var config = new StandingsConfig
        {
            Tiebreakers = ["Points", "Wins", "HeadToHeadPoints"]
        };
        var source = new SeedingConfigurationStandingsSource($"Season:{SeasonID}:1-4", config);

        var teams = (await source.GetTeamsAsync(MockSeasonGames().Object)).ToList();

        teams.Should().Equal([TeamD, TeamB, TeamA, TeamC],
            "seeds must follow the configured head-to-head tiebreaker, not the default run differential");
    }

    [Fact]
    public async Task Parse_ThreadsConfigThroughToSeedAssignment()
    {
        var config = new StandingsConfig
        {
            Tiebreakers = ["Points", "Wins", "HeadToHeadPoints"]
        };

        var seeds = await SeedingConfiguration.Parse(
            $"1-4,Standings,Season:{SeasonID}:1-4", MockSeasonGames().Object, config);

        seeds[1].Should().Be(TeamD);
        seeds[2].Should().Be(TeamB, "the head-to-head winner takes the higher seed");
        seeds[3].Should().Be(TeamA);
        seeds[4].Should().Be(TeamC);
    }
}
