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

    // --- Champion: decided by the Historical flag alone -------------------------

    static readonly Team TeamC = TestDataHelper.MakeTeam(3, "Gammas", "GAM");
    static readonly Team TeamD = TestDataHelper.MakeTeam(4, "Deltas", "DEL");

    static RoundSeries Series(long number, Team winner, Team loser, bool decided = true) => new()
    {
        ID = number,
        Number = number,
        Format = "Best of",
        HostOrder = "121",
        Matchup = "#1-#2",
        Spots = (new TournamentSeriesSpot('#', 1, winner), new TournamentSeriesSpot('#', 2, loser)),
        Winner = decided ? winner : null,
        Loser = decided ? loser : null,
        Games = [],
    };

    static TournamentBracket Bracket(long id, string name, bool historical, params RoundSeries[] series) => new()
    {
        ID = id,
        Name = name,
        Format = "Fixed",
        Historical = historical,
        Rounds = [new BracketRound { ID = id * 10, Name = "Finals", Series = [.. series] }],
    };

    static TournamentRoundRobin Pool(long id, string name, bool historical, Standings? standings) => new()
    {
        ID = id,
        Name = name,
        Historical = historical,
        Standings = standings,
    };

    static Year PlayoffYear(IEnumerable<TournamentBracket> brackets, IEnumerable<TournamentRoundRobin>? pools = null)
    {
        var tournament = new Tournament { ID = 1, SeasonID = 2, Brackets = [.. brackets], RoundRobins = [.. pools ?? []] };
        var playoffs = new Season
        {
            ID = 2,
            Year = 2025,
            Subseason = "Playoffs",
            StartDate = new DateTime(2025, 8, 18),
            Tournaments = [tournament],
        };
        return new Year(2025, [playoffs]);
    }

    [Fact]
    public void OneHistoricalBracket_ItsWinnerIsChampion_EvenWhenItIsNotTheFirstBracket()
    {
        // The 2025 shape: Main unflagged, Consolation flagged. The old code
        // ignored the flag here and reported the first bracket's winner.
        var year = PlayoffYear(
        [
            Bracket(1, "Main", historical: false, Series(1, TeamA, TeamB)),
            Bracket(2, "Consolation", historical: true, Series(2, TeamC, TeamD)),
        ]);

        var dto = HistoryYearDto.From(year);

        dto.PlayoffsComplete.Should().BeTrue();
        dto.Champion.Should().Be("Gammas City Gammas");
        dto.ChampionAbbreviation.Should().Be("GAM");
    }

    [Fact]
    public void NoHistoricalBracketOrPool_NoChampion()
    {
        var year = PlayoffYear(
        [
            Bracket(1, "Main", historical: false, Series(1, TeamA, TeamB)),
            Bracket(2, "Consolation", historical: false, Series(2, TeamC, TeamD)),
        ]);

        var dto = HistoryYearDto.From(year);

        dto.PlayoffsComplete.Should().BeTrue();
        dto.Champion.Should().BeNull();
        dto.ChampionAbbreviation.Should().BeNull();
    }

    [Fact]
    public void SeveralHistorical_AreListedByName()
    {
        var year = PlayoffYear(
        [
            Bracket(1, "Main", historical: true, Series(1, TeamA, TeamB)),
            Bracket(2, "Consolation", historical: true, Series(2, TeamC, TeamD)),
        ]);

        var dto = HistoryYearDto.From(year);

        dto.Champion.Should().Be("Main: Alphas City Alphas; Consolation: Gammas City Gammas");
        dto.ChampionAbbreviation.Should().BeNull();
    }

    [Fact]
    public void HistoricalPool_NamesItsStandingsLeader()
    {
        var poolGames = new List<Game>
        {
            TestDataHelper.MakeGame(TeamD, TeamC, "Played", scoreHost: 7, scoreVisitor: 2),
            TestDataHelper.MakeGame(TeamC, TeamD, "Played", scoreHost: 1, scoreVisitor: 4),
        };
        var year = PlayoffYear(
            [Bracket(1, "Main", historical: false, Series(1, TeamA, TeamB))],
            [Pool(5, "B Side", historical: true, new Standings(poolGames))]);

        var dto = HistoryYearDto.From(year);

        dto.Champion.Should().Be("Deltas City Deltas");
        dto.ChampionAbbreviation.Should().Be("DEL");
    }

    [Fact]
    public void HistoricalPoolWithoutStandings_IsIgnored()
    {
        var year = PlayoffYear(
            [Bracket(1, "Main", historical: true, Series(1, TeamA, TeamB))],
            [Pool(5, "B Side", historical: true, standings: null)]);

        var dto = HistoryYearDto.From(year);

        dto.Champion.Should().Be("Alphas City Alphas");
    }

    [Fact]
    public void UnfinishedPlayoffs_NoChampionYet_EvenWithAHistoricalBracket()
    {
        var year = PlayoffYear(
        [
            Bracket(1, "Main", historical: true, Series(1, TeamA, TeamB)),
            Bracket(2, "Consolation", historical: false, Series(2, TeamC, TeamD, decided: false)),
        ]);

        var dto = HistoryYearDto.From(year);

        dto.PlayoffsComplete.Should().BeFalse();
        dto.Champion.Should().BeNull();
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
