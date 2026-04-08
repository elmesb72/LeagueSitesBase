using FluentAssertions;

namespace LeagueSitesBase.Tests;

public class StandingsTests
{
    static readonly Team TeamA = TestDataHelper.MakeTeam(1, "Alphas", "ALP");
    static readonly Team TeamB = TestDataHelper.MakeTeam(2, "Betas", "BET");
    static readonly Team TeamC = TestDataHelper.MakeTeam(3, "Gammas", "GAM");

    [Fact]
    public void EmptyGameList_ProducesEmptyStandings()
    {
        var standings = new Standings([]);
        standings.Should().BeEmpty();
    }

    [Fact]
    public void SinglePlayedGame_ProducesCorrectRecords()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 5, scoreVisitor: 3),
        };

        var standings = new Standings(games);

        standings.Should().HaveCount(2);
        standings[TeamA].Wins.Should().Be(1);
        standings[TeamA].Losses.Should().Be(0);
        standings[TeamA].RunsScored.Should().Be(5);
        standings[TeamA].RunsAllowed.Should().Be(3);
        standings[TeamB].Wins.Should().Be(0);
        standings[TeamB].Losses.Should().Be(1);
    }

    [Fact]
    public void TiedGame_RecordedAsTie()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 4, scoreVisitor: 4),
        };

        var standings = new Standings(games);

        standings[TeamA].Ties.Should().Be(1);
        standings[TeamA].Wins.Should().Be(0);
        standings[TeamB].Ties.Should().Be(1);
    }

    [Fact]
    public void Points_CalculatedCorrectly()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 5, scoreVisitor: 3),
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 2, scoreVisitor: 2),
        };

        var standings = new Standings(games);

        // Default: wins=2pts, ties=1pt, losses=0pts
        standings[TeamA].Points.Should().Be(3); // 1 win (2) + 1 tie (1)
        standings[TeamB].Points.Should().Be(1); // 1 tie (1)
    }

    [Fact]
    public void RunDifferential_CalculatedCorrectly()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 7, scoreVisitor: 2),
            TestDataHelper.MakeGame(TeamB, TeamA, "Played", scoreHost: 3, scoreVisitor: 4),
        };

        var standings = new Standings(games);

        standings[TeamA].RunsScored.Should().Be(11); // 7 + 4
        standings[TeamA].RunsAllowed.Should().Be(5);  // 2 + 3
        standings[TeamA].RunDifferential.Should().Be(6);
        standings[TeamB].RunDifferential.Should().Be(-6);
    }

    [Fact]
    public void Ordering_ByPointsThenWinsThenRunDiffThenName()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 5, scoreVisitor: 3),
            TestDataHelper.MakeGame(TeamA, TeamC, "Played", scoreHost: 5, scoreVisitor: 3),
            TestDataHelper.MakeGame(TeamB, TeamC, "Played", scoreHost: 5, scoreVisitor: 3),
        };

        var standings = new Standings(games);

        standings.Keys.First().Should().Be(TeamA, "TeamA has 2 wins");
        standings.Keys.Last().Should().Be(TeamC, "TeamC has 0 wins");
    }

    [Fact]
    public void ForfeitHome_ScoresCorrectly()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Forfeit (Home)", scoreHost: null, scoreVisitor: null),
        };

        var standings = new Standings(games);

        // Forfeit (Home) means home team forfeits: visitor gets 7-0
        standings[TeamB].Wins.Should().Be(1);
        standings[TeamB].RunsScored.Should().Be(7);
        standings[TeamA].Losses.Should().Be(1);
        standings[TeamA].RunsScored.Should().Be(0);
    }

    [Fact]
    public void ForfeitAway_ScoresCorrectly()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Forfeit (Away)", scoreHost: null, scoreVisitor: null),
        };

        var standings = new Standings(games);

        // Forfeit (Away) means visitor forfeits: host gets 7-0
        standings[TeamA].Wins.Should().Be(1);
        standings[TeamA].RunsScored.Should().Be(7);
        standings[TeamB].Losses.Should().Be(1);
    }

    [Fact]
    public void UpcomingGames_SkippedInStandings()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 5, scoreVisitor: 3),
            TestDataHelper.MakeGame(TeamA, TeamB, "Upcoming"),
        };

        var standings = new Standings(games);

        standings[TeamA].GamesPlayed.Should().Be(1);
    }

    [Fact]
    public void CalculateStreaks_WinStreak()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 5, scoreVisitor: 3, date: DateTime.Today.AddDays(-2)),
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 4, scoreVisitor: 2, date: DateTime.Today.AddDays(-1)),
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 6, scoreVisitor: 1, date: DateTime.Today),
        };

        var standings = new Standings(games);
        standings.CalculateStreaks();

        standings[TeamA].Streak.Should().Be("W3");
        standings[TeamB].Streak.Should().Be("L3");
    }

    [Fact]
    public void CalculateStreaks_BrokenStreak()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 5, scoreVisitor: 3, date: DateTime.Today.AddDays(-2)),
            TestDataHelper.MakeGame(TeamB, TeamA, "Played", scoreHost: 5, scoreVisitor: 3, date: DateTime.Today.AddDays(-1)),
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 6, scoreVisitor: 1, date: DateTime.Today),
        };

        var standings = new Standings(games);
        standings.CalculateStreaks();

        standings[TeamA].Streak.Should().Be("W1");
    }

    [Fact]
    public void CalculateStreaks_NoGames_ReturnsDash()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Upcoming"),
        };

        var standings = new Standings(games);
        standings.CalculateStreaks();

        standings[TeamA].Streak.Should().Be("-");
    }

    [Fact]
    public void ToDto_ProducesCorrectShape()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 5, scoreVisitor: 3),
        };

        var standings = new Standings(games);
        standings.CalculateStreaks();
        var dto = standings.ToDto();

        dto.Should().HaveCount(2);
        dto.First().Team.Name.Should().Be("Alphas");
        dto.First().Wins.Should().Be(1);
        dto.First().Record.Should().Be("(1-0-0)");
    }

    [Fact]
    public void GameWithNullScores_Skipped()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: null, scoreVisitor: null),
        };

        var standings = new Standings(games);

        standings[TeamA].GamesPlayed.Should().Be(0);
        standings[TeamB].GamesPlayed.Should().Be(0);
    }

    [Fact]
    public void MissingTeamData_ThrowsException()
    {
        var game = new Game
        {
            HostTeam = null,
            VisitingTeam = TeamB,
            Status = new GameStatus { Name = "Played" },
        };

        var act = () => new Standings([game]);

        act.Should().Throw<Exception>().WithMessage("Game object missing Team data");
    }
}
