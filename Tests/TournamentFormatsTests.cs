using FluentAssertions;

namespace LeagueSitesBackend.Tests;

public class TournamentFormatsTests
{
    // ------------------------------------------------------------------ Matchup

    [Theory]
    [InlineData("#1-#8", SeriesSpotRef.Seed, 1, SeriesSpotRef.Seed, 8)]
    [InlineData("#2-#7", SeriesSpotRef.Seed, 2, SeriesSpotRef.Seed, 7)]
    [InlineData("w1-w2", SeriesSpotRef.Winner, 1, SeriesSpotRef.Winner, 2)]
    [InlineData("l3-l4", SeriesSpotRef.Loser, 3, SeriesSpotRef.Loser, 4)]
    [InlineData("r1-r4", SeriesSpotRef.Reseed, 1, SeriesSpotRef.Reseed, 4)]
    [InlineData("#1-r3", SeriesSpotRef.Seed, 1, SeriesSpotRef.Reseed, 3)]
    [InlineData("#9-#10", SeriesSpotRef.Seed, 9, SeriesSpotRef.Seed, 10)]
    public void ParseMatchup_ReadsBothSpots(
        string matchup, string type1, int number1, string type2, int number2)
    {
        var (spot1, spot2) = TournamentFormats.ParseMatchup(matchup);

        spot1.Should().Be(new SeriesSpotRef(type1, number1));
        spot2.Should().Be(new SeriesSpotRef(type2, number2));
    }

    // Every matchup encoding present in a real league database.
    [Theory]
    [InlineData("#1-#8")]
    [InlineData("#2-#7")]
    [InlineData("#3-#6")]
    [InlineData("#4-#5")]
    [InlineData("#1-#4")]
    [InlineData("#2-#3")]
    [InlineData("#6-#7")]
    [InlineData("#3-#7")]
    [InlineData("#2-#6")]
    [InlineData("#5-#6")]
    [InlineData("#9-#10")]
    [InlineData("#1-#2")]
    [InlineData("w1-w2")]
    [InlineData("r1-r4")]
    [InlineData("r2-r3")]
    [InlineData("r1-r2")]
    [InlineData("#1-r3")]
    public void Matchup_RoundTrips(string matchup)
    {
        var (spot1, spot2) = TournamentFormats.ParseMatchup(matchup);
        TournamentFormats.SerializeMatchup(spot1, spot2).Should().Be(matchup);
    }

    [Theory]
    [InlineData("#1")]           // only one spot
    [InlineData("#1-#2-#3")]     // three spots
    [InlineData("x1-x2")]        // unknown source
    [InlineData("#-#2")]         // missing number
    [InlineData("#0-#2")]        // seeds start at 1
    [InlineData("#a-#2")]        // non-numeric
    [InlineData("")]
    public void ParseMatchup_RejectsMalformed(string matchup)
    {
        var act = () => TournamentFormats.ParseMatchup(matchup);
        act.Should().Throw<TournamentFormatException>();
    }

    // ---------------------------------------------------------------- HostOrder

    [Theory]
    [InlineData("1", 1)]
    [InlineData("121", 3)]
    [InlineData("12121", 5)]
    [InlineData("1122121", 7)]
    public void HostOrder_RoundTripsAndReportsLength(string hostOrder, int expectedLength)
    {
        var hosts = TournamentFormats.ParseHostOrder(hostOrder);

        hosts.Should().HaveCount(expectedLength);
        TournamentFormats.SerializeHostOrder(hosts).Should().Be(hostOrder);
        TournamentFormats.SeriesLength(hostOrder).Should().Be(expectedLength);
    }

    [Fact]
    public void ParseHostOrder_NamesWhichSpotHostsEachGame()
    {
        // "121" is the higher seed at home for games 1 and 3.
        TournamentFormats.ParseHostOrder("121").Should().Equal(1, 2, 1);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("121", 2)]
    [InlineData("12121", 3)]
    [InlineData("1122121", 4)]
    public void WinsRequired_IsAMajorityOfTheSeries(string hostOrder, int expected)
    {
        TournamentFormats.WinsRequired(hostOrder).Should().Be(expected);
    }

    [Theory]
    [InlineData("113")]
    [InlineData("120")]
    [InlineData("abc")]
    [InlineData("")]
    public void ParseHostOrder_RejectsAnythingButOnesAndTwos(string hostOrder)
    {
        var act = () => TournamentFormats.ParseHostOrder(hostOrder);
        act.Should().Throw<TournamentFormatException>();
    }

    [Fact]
    public void SerializeHostOrder_RejectsEmptySeries()
    {
        var act = () => TournamentFormats.SerializeHostOrder([]);
        act.Should().Throw<TournamentFormatException>();
    }

    // ---------------------------------------------------------------- Seeding

    [Fact]
    public void ParseSeeding_ReadsASingleStandingsGroup()
    {
        var groups = TournamentFormats.ParseSeeding("1-8,Standings,Season:13:1-8");

        groups.Should().HaveCount(1);
        groups[0].Should().Be(new SeedGroup(
            1, 8, SeedGroup.ResultStandings, SeedGroup.SourceSeason, 13, 1, 8));
    }

    [Fact]
    public void ParseSeeding_ReadsMultipleGroupsFromDifferentSources()
    {
        // A real consolation pool: two knocked-out teams plus a seeding-round result.
        var configuration =
            "1-1,Losers,BracketRound:15:1-1;3-3,Losers,BracketRound:15:3-3;6-6,Standings,BracketRound:18:2-2";

        var groups = TournamentFormats.ParseSeeding(configuration);

        groups.Should().HaveCount(3);
        groups[0].Result.Should().Be(SeedGroup.ResultLosers);
        groups[0].SourceType.Should().Be(SeedGroup.SourceBracketRound);
        groups[2].OutputStart.Should().Be(6);
        groups[2].RankStart.Should().Be(2);
    }

    [Theory]
    [InlineData("1-8,Standings,Season:13:1-8")]
    [InlineData("9-10,Standings,Season:13:9-10")]
    [InlineData("1-1,Standings,TournamentRoundRobin:3:1-1;2-2,Standings,TournamentRoundRobin:4:1-1")]
    [InlineData("1-1,Losers,BracketRound:15:1-1;3-3,Losers,BracketRound:15:3-3;6-6,Standings,BracketRound:18:2-2")]
    [InlineData("2-2,Losers,BracketRound:15:2-2;4-4,Losers,BracketRound:15:4-4;5-5,Standings,BracketRound:18:1-1")]
    public void Seeding_RoundTrips(string configuration)
    {
        var groups = TournamentFormats.ParseSeeding(configuration);
        TournamentFormats.SerializeSeeding(groups).Should().Be(configuration);
    }

    [Fact]
    public void ParseSeeding_TreatsMissingConfigurationAsNoRules()
    {
        TournamentFormats.ParseSeeding(null).Should().BeEmpty();
        TournamentFormats.ParseSeeding("").Should().BeEmpty();
    }

    [Fact]
    public void SerializeSeeding_ReturnsNullForNoRules()
    {
        TournamentFormats.SerializeSeeding([]).Should().BeNull();
    }

    [Fact]
    public void ValidateSeedGroups_RejectsSeedClaimedTwice()
    {
        List<SeedGroup> groups = [
            new(1, 4, SeedGroup.ResultStandings, SeedGroup.SourceSeason, 13, 1, 4),
            new(4, 6, SeedGroup.ResultStandings, SeedGroup.SourceSeason, 13, 4, 6),
        ];

        var act = () => TournamentFormats.ValidateSeedGroups(groups);
        act.Should().Throw<TournamentFormatException>().WithMessage("*Seed 4*more than one*");
    }

    [Fact]
    public void ValidateSeedGroups_RejectsMismatchedRangeSizes()
    {
        // Four seeds to fill but only two teams offered.
        List<SeedGroup> groups = [
            new(1, 4, SeedGroup.ResultStandings, SeedGroup.SourceSeason, 13, 1, 2),
        ];

        var act = () => TournamentFormats.ValidateSeedGroups(groups);
        act.Should().Throw<TournamentFormatException>().WithMessage("*2 team(s)*needs 4*");
    }

    [Fact]
    public void ValidateSeedGroups_RejectsLosersFromANonRoundSource()
    {
        List<SeedGroup> groups = [
            new(1, 1, SeedGroup.ResultLosers, SeedGroup.SourceSeason, 13, 1, 1),
        ];

        var act = () => TournamentFormats.ValidateSeedGroups(groups);
        act.Should().Throw<TournamentFormatException>().WithMessage("*cannot be used with Losers*");
    }

    [Theory]
    [InlineData("1-8,Standings")]                       // too few parts
    [InlineData("1-8,Sideways,Season:13:1-8")]          // unknown result
    [InlineData("1-8,Standings,Season:13")]             // source missing rank range
    [InlineData("1-8,Standings,Season:abc:1-8")]        // non-numeric source id
    [InlineData("8-1,Standings,Season:13:1-8")]         // inverted output range
    public void ParseSeeding_RejectsMalformed(string configuration)
    {
        var act = () => TournamentFormats.ParseSeeding(configuration);
        act.Should().Throw<TournamentFormatException>();
    }

    // ----------------------------------------------------------------- Formats

    [Fact]
    public void ValidateSeriesFormat_RejectsEvenLengthBestOfSeries()
    {
        var act = () => TournamentFormats.ValidateSeriesFormat(TournamentFormats.SeriesFormatBestOf, "12");
        act.Should().Throw<TournamentFormatException>().WithMessage("*odd number of games*");
    }

    [Fact]
    public void ValidateSeriesFormat_AcceptsRealFormats()
    {
        var act = () => TournamentFormats.ValidateSeriesFormat(TournamentFormats.SeriesFormatBestOf, "121");
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateSeriesFormat_RejectsUnknownFormat()
    {
        var act = () => TournamentFormats.ValidateSeriesFormat("Sudden Death", "1");
        act.Should().Throw<TournamentFormatException>();
    }

    [Theory]
    [InlineData("Fixed")]
    [InlineData("Re-seed")]
    public void ValidateBracketFormat_AcceptsRealFormats(string format)
    {
        var act = () => TournamentFormats.ValidateBracketFormat(format);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateBracketFormat_RejectsUnknownFormat()
    {
        var act = () => TournamentFormats.ValidateBracketFormat("Ladder");
        act.Should().Throw<TournamentFormatException>();
    }
}
