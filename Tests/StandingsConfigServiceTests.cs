using FluentAssertions;

namespace LeagueSitesBackend.Tests;

/// <summary>
/// The parsing safety net (Requirement 6.2): whatever is actually stored in
/// SiteConfig.StandingsJson — absent, empty, partial, or garbage — must
/// resolve to a usable config, defaulting to the historic rules.
/// </summary>
public class StandingsConfigServiceTests
{
    static void ShouldBeDefaults(StandingsConfig config)
    {
        config.WinsValue.Should().Be(2);
        config.TiesValue.Should().Be(1);
        config.LossesValue.Should().Be(0);
        config.ForfeitWinnerScore.Should().Be(7);
        config.ForfeitLoserScore.Should().Be(0);
        config.Tiebreakers.Should().Equal("Points", "Wins", "RunDifferential");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    public void AbsentOrEmptyJson_YieldsDefaults(string? json)
    {
        ShouldBeDefaults(StandingsConfigService.Parse(json));
    }

    [Fact]
    public void GarbageJson_YieldsDefaults_WithoutThrowing()
    {
        ShouldBeDefaults(StandingsConfigService.Parse("not json {{{"));
    }

    [Fact]
    public void PartialJson_FillsRemainingDefaults()
    {
        var config = StandingsConfigService.Parse("{\"winsValue\":3}");

        config.WinsValue.Should().Be(3);
        config.TiesValue.Should().Be(1);
        config.ForfeitWinnerScore.Should().Be(7);
        config.Tiebreakers.Should().Equal("Points", "Wins", "RunDifferential");
    }

    [Fact]
    public void FullJson_RoundTripsAllValues()
    {
        var json = "{\"winsValue\":3,\"tiesValue\":2,\"lossesValue\":1,"
            + "\"forfeitWinnerScore\":9,\"forfeitLoserScore\":1,"
            + "\"tiebreakers\":[\"Wins\",\"HeadToHeadPoints\",\"HeadToHeadRunDifferential\"]}";

        var config = StandingsConfigService.Parse(json);

        config.WinsValue.Should().Be(3);
        config.TiesValue.Should().Be(2);
        config.LossesValue.Should().Be(1);
        config.ForfeitWinnerScore.Should().Be(9);
        config.ForfeitLoserScore.Should().Be(1);
        config.Tiebreakers.Should().Equal("Wins", "HeadToHeadPoints", "HeadToHeadRunDifferential");
    }

    [Fact]
    public void UnknownTiebreakers_AreFilteredOut()
    {
        var config = StandingsConfigService.Parse("{\"tiebreakers\":[\"Wins\",\"Bogus\"]}");

        config.Tiebreakers.Should().Equal("Wins");
    }

    [Fact]
    public void AllUnknownTiebreakers_FallBackToDefaultRule()
    {
        var config = StandingsConfigService.Parse("{\"tiebreakers\":[\"Bogus\",\"AlsoNotReal\"]}");

        config.Tiebreakers.Should().Equal("Points", "Wins", "RunDifferential");
    }

    [Theory]
    [InlineData("{\"tiebreakers\":[]}")]
    [InlineData("{\"tiebreakers\":null}")]
    public void EmptyOrNullTiebreakers_FallBackToDefaultRule(string json)
    {
        StandingsConfigService.Parse(json).Tiebreakers
            .Should().Equal("Points", "Wins", "RunDifferential");
    }

    [Fact]
    public void Registry_HasNineComparators_WithDescriptions()
    {
        StandingsComparators.All.Should().HaveCount(9);
        StandingsComparators.All.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Description));
        StandingsComparators.All.Select(c => c.Name).Should().OnlyHaveUniqueItems();
        StandingsComparators.Find("headtoheadpoints").Should().NotBeNull("lookups are case-insensitive");
    }
}

/// <summary>
/// Storage-time validation (Requirement 5.3): the Webmaster save path must
/// reject every malformed config with a message naming the problem.
/// </summary>
public class StandingsConfigValidationTests
{
    static StandingsConfig Valid() => new()
    {
        WinsValue = 3,
        TiesValue = 1,
        LossesValue = 0,
        ForfeitWinnerScore = 7,
        ForfeitLoserScore = 0,
        Tiebreakers = ["Wins", "HeadToHeadPoints", "HeadToHeadRunDifferential"],
    };

    [Fact]
    public void ValidConfig_HasNoProblems()
    {
        StandingsConfigService.Validate(Valid()).Should().BeEmpty();
    }

    [Fact]
    public void DefaultConfig_HasNoProblems()
    {
        StandingsConfigService.Validate(new StandingsConfig()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(101)]
    [InlineData(-101)]
    public void PointValuesOutOfRange_AreRejected(int value)
    {
        var config = Valid();
        config.WinsValue = value;

        StandingsConfigService.Validate(config)
            .Should().ContainSingle(p => p.Contains("Win points"));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(100, 0)]
    public void ForfeitScoresOutOfRange_AreRejected(int winner, int loser)
    {
        var config = Valid();
        config.ForfeitWinnerScore = winner;
        config.ForfeitLoserScore = loser;

        StandingsConfigService.Validate(config)
            .Should().Contain(p => p.Contains("Forfeit winner score"));
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(3, 7)]
    public void ForfeitWinnerNotAboveLoser_IsRejected(int winner, int loser)
    {
        var config = Valid();
        config.ForfeitWinnerScore = winner;
        config.ForfeitLoserScore = loser;

        StandingsConfigService.Validate(config)
            .Should().Contain(p => p.Contains("must be higher than the loser score"));
    }

    [Fact]
    public void EmptyTiebreakers_IsRejected()
    {
        var config = Valid();
        config.Tiebreakers = [];

        StandingsConfigService.Validate(config)
            .Should().ContainSingle(p => p.Contains("At least one tiebreaker"));
    }

    [Fact]
    public void UnknownTiebreaker_IsRejectedByName()
    {
        var config = Valid();
        config.Tiebreakers = ["Wins", "CoinFlip"];

        StandingsConfigService.Validate(config)
            .Should().ContainSingle(p => p.Contains("'CoinFlip'"));
    }

    [Fact]
    public void DuplicateTiebreakers_AreRejected_CaseInsensitively()
    {
        var config = Valid();
        config.Tiebreakers = ["Wins", "wins"];

        StandingsConfigService.Validate(config)
            .Should().ContainSingle(p => p.Contains("more than once"));
    }

    [Fact]
    public void MultipleProblems_AreAllReported()
    {
        var config = new StandingsConfig
        {
            WinsValue = 500,
            ForfeitWinnerScore = 0,
            ForfeitLoserScore = 0,
            Tiebreakers = ["Bogus"],
        };

        var problems = StandingsConfigService.Validate(config);

        problems.Should().HaveCountGreaterThanOrEqualTo(3,
            "every violation is reported at once so the UI can show them all");
    }
}
