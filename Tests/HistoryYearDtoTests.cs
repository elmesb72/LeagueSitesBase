using FluentAssertions;

namespace LeagueSitesBackend.Tests;

public class HistoryYearDtoTests
{
    static readonly Team TeamA = TestDataHelper.MakeTeam(1, "Alphas", "ALP");
    static readonly Team TeamB = TestDataHelper.MakeTeam(2, "Betas", "BET");

    static Season MakeRegularSeason(long year, List<Game> games) => new()
    {
        ID = 1,
        Year = year,
        Subseason = "Regular Season",
        StartDate = new DateTime((int)year, 5, 1),
        Games = games,
    };

    [Fact]
    public void ExceptionYear_HasDescriptionOnly()
    {
        var year = new Year(2020, "Season cancelled");
        var dto = HistoryYearDto.From(year);

        dto.CalendarYear.Should().Be(2020);
        dto.ExceptionYearDescription.Should().Be("Season cancelled");
        dto.Champion.Should().BeNull();
        dto.BestRecord.Should().BeNull();
        dto.PlayoffsComplete.Should().BeFalse();
        dto.RegularSeasonComplete.Should().BeFalse();
    }

    [Fact]
    public void CompletedRegularSeason_HasBestRecord()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 5, scoreVisitor: 3),
            TestDataHelper.MakeGame(TeamB, TeamA, "Played", scoreHost: 2, scoreVisitor: 4),
        };
        var season = MakeRegularSeason(2024, games);
        var year = new Year(2024, [season]);

        var dto = HistoryYearDto.From(year);

        dto.RegularSeasonComplete.Should().BeTrue();
        dto.BestRecord.Should().Be("Alphas City Alphas");
        dto.BestRecordAbbreviation.Should().Be("ALP");
        dto.BestRecordResults.Should().Be("(2-0-0)");
    }

    [Fact]
    public void IncompleteRegularSeason_NoBestRecord()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 5, scoreVisitor: 3),
            TestDataHelper.MakeGame(TeamA, TeamB, "Upcoming"),
        };
        var season = MakeRegularSeason(2024, games);
        var year = new Year(2024, [season]);

        var dto = HistoryYearDto.From(year);

        dto.RegularSeasonComplete.Should().BeFalse();
        dto.BestRecord.Should().BeNull();
    }

    [Fact]
    public void NoSeasons_HandlesGracefully()
    {
        var year = new Year(2025, []);

        var dto = HistoryYearDto.From(year);

        dto.CalendarYear.Should().Be(2025);
        dto.RegularSeasonComplete.Should().BeFalse();
        dto.PlayoffsComplete.Should().BeFalse();
        dto.Champion.Should().BeNull();
        dto.BestRecord.Should().BeNull();
    }
}
