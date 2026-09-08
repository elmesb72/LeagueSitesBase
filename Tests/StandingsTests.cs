using FluentAssertions;

namespace LeagueSitesBackend.Tests;

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

public class StandingsConfigurableRulesTests
{
    static readonly Team TeamA = TestDataHelper.MakeTeam(1, "Alphas", "ALP");
    static readonly Team TeamB = TestDataHelper.MakeTeam(2, "Betas", "BET");
    static readonly Team TeamC = TestDataHelper.MakeTeam(3, "Gammas", "GAM");
    static readonly Team TeamD = TestDataHelper.MakeTeam(4, "Deltas", "DEL");

    static StandingsConfig RuleOf(params string[] tiebreakers) =>
        new() { Tiebreakers = [.. tiebreakers] };

    [Fact]
    public void CustomPointValues_ChangePointsAndOrder()
    {
        // A: 1 win. B: 2 ties. Default (2/1/0): A=2, B=2 — tied on points.
        // Soccer-style 3/1/0: A=3, B=2 — A clearly first.
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamC, "Played", scoreHost: 2, scoreVisitor: 1),
            TestDataHelper.MakeGame(TeamB, TeamC, "Played", scoreHost: 1, scoreVisitor: 1),
            TestDataHelper.MakeGame(TeamB, TeamD, "Played", scoreHost: 0, scoreVisitor: 0),
        };

        var config = new StandingsConfig { WinsValue = 3, TiesValue = 1, LossesValue = 0 };
        var standings = new Standings(games, config);

        standings[TeamA].Points.Should().Be(3);
        standings[TeamB].Points.Should().Be(2);
        standings.Keys.First().Should().Be(TeamA);
    }

    [Fact]
    public void CustomForfeitScore_ReflectedInRunsAndDifferential()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Forfeit (Home)", scoreHost: null, scoreVisitor: null),
        };

        var config = new StandingsConfig { ForfeitWinnerScore = 9, ForfeitLoserScore = 1 };
        var standings = new Standings(games, config);

        standings[TeamB].Wins.Should().Be(1);
        standings[TeamB].RunsScored.Should().Be(9);
        standings[TeamB].RunsAllowed.Should().Be(1);
        standings[TeamA].RunsScored.Should().Be(1);
        standings[TeamA].RunDifferential.Should().Be(-8);
    }

    [Fact]
    public void HeadToHead_TwoWayTie_WinnerOfMeetingRanksFirst()
    {
        // D sweeps (clear first), C loses out (clear last).
        // A and B: tied on points and wins. A has a far better overall run
        // differential (+9 vs 0), but B won the head-to-head meeting.
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamC, "Played", scoreHost: 10, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamB, TeamA, "Played", scoreHost: 2, scoreVisitor: 1),
            TestDataHelper.MakeGame(TeamD, TeamB, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamD, TeamC, "Played", scoreHost: 5, scoreVisitor: 0),
        };

        // Default (canonical) rules break the tie head-to-head: B beat A.
        var byDefault = new Standings(games);
        byDefault.Keys.ElementAt(1).Should().Be(TeamB, "default rules rank the head-to-head winner first");
        byDefault.Keys.ElementAt(2).Should().Be(TeamA);

        // A season stored with the pre-2026 rules keeps its old ordering:
        // overall run differential puts A above B.
        var byOldRules = new Standings(games, RuleOf("Points", "Wins", "RunDifferential"));
        byOldRules.Keys.ElementAt(1).Should().Be(TeamA, "the old explicit rules break the tie by overall run differential");
        byOldRules.Keys.ElementAt(2).Should().Be(TeamB);
    }

    [Fact]
    public void HeadToHead_ThreeWayTie_SubgroupsRecomputeAgainstEachOtherOnly()
    {
        // A, B, C all finish with 4 wins (D is the fodder).
        // Head-to-head points among {A,B,C}: A won all four of its trio games
        // (8 pts), B and C split their pair (2 pts each) — A splits off first.
        // The remaining {B,C} subgroup must then be compared on THEIR OWN
        // games only: C beat B 5-0 and lost 0-1, so C is +4 pairwise.
        // A naive implementation that keeps using the three-team game set
        // would rank B above C (B was only -6 in the trio; C was -16, having
        // been crushed twice by A).
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamA, TeamC, "Played", scoreHost: 10, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamA, TeamC, "Played", scoreHost: 10, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamB, TeamC, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamC, TeamB, "Played", scoreHost: 5, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamB, TeamD, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamB, TeamD, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamB, TeamD, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamC, TeamD, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamC, TeamD, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamC, TeamD, "Played", scoreHost: 1, scoreVisitor: 0),
        };

        var standings = new Standings(games,
            RuleOf("Wins", "HeadToHeadPoints", "HeadToHeadRunDifferential"));

        standings.Keys.ElementAt(0).Should().Be(TeamA, "A won every game against the other tied teams");
        standings.Keys.ElementAt(1).Should().Be(TeamC, "C out-scored B in the games between just those two");
        standings.Keys.ElementAt(2).Should().Be(TeamB);
        standings.Keys.ElementAt(3).Should().Be(TeamD);
    }

    [Fact]
    public void HeadToHead_NoMeetings_FallsThroughToNextComparator()
    {
        // A and B are tied but never played each other; head-to-head must
        // leave the tie intact so the next comparator (runs scored) decides.
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamC, "Played", scoreHost: 5, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamB, TeamD, "Played", scoreHost: 6, scoreVisitor: 0),
        };

        var standings = new Standings(games, RuleOf("Wins", "HeadToHeadPoints", "RunsScored"));

        standings.Keys.First().Should().Be(TeamB, "with no meetings, the tie falls through to runs scored");
    }

    [Fact]
    public void HeadToHead_NoMeetings_NoLaterComparator_FallsBackToName()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamB, TeamD, "Played", scoreHost: 5, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamA, TeamC, "Played", scoreHost: 5, scoreVisitor: 0),
        };

        var standings = new Standings(games, RuleOf("Wins", "HeadToHeadPoints"));

        // B was inserted first (its game is listed first) — alphabetical
        // fallback must still put Alphas ahead of Betas.
        standings.Keys.First().Should().Be(TeamA);
    }

    [Fact]
    public void WinPercentage_RanksRatioOverTotals()
    {
        // A: 3-0 (1.000). B: 3-1 (0.750). By points B would tie A (6 each);
        // by win percentage A is clearly first.
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamC, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamA, TeamC, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamB, TeamC, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamB, TeamC, "Played", scoreHost: 1, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamB, TeamC, "Played", scoreHost: 1, scoreVisitor: 0),
        };

        var standings = new Standings(games, RuleOf("WinPercentage"));

        standings.Keys.ElementAt(0).Should().Be(TeamA);
        standings.Keys.ElementAt(1).Should().Be(TeamB);
    }

    [Fact]
    public void FewestRunsAllowed_OrdersAscending()
    {
        // Runs allowed: C=0, A=2, B=5, D=10. Under a fewest-runs-allowed-only
        // rule the order is exactly ascending by runs allowed — A ranks above
        // B despite losing its game while B won, proving the direction.
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamC, TeamA, "Played", scoreHost: 2, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamB, TeamD, "Played", scoreHost: 10, scoreVisitor: 5),
        };

        var standings = new Standings(games, RuleOf("FewestRunsAllowed"));

        standings.Keys.Should().Equal(TeamC, TeamA, TeamB, TeamD);
    }

    [Fact]
    public void EverythingEqual_FallsBackToAlphabetical()
    {
        // One tied game between Betas (host, inserted first) and Alphas:
        // identical on every metric, so the name fallback decides.
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamB, TeamA, "Played", scoreHost: 3, scoreVisitor: 3),
        };

        var standings = new Standings(games, RuleOf("Points", "Wins", "HeadToHeadPoints"));

        standings.Keys.First().Should().Be(TeamA, "the final fallback is alphabetical, not insertion order");
    }

    [Fact]
    public void UnknownOrEmptyTiebreakers_BehaveAsDefaultRules()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamB, "Played", scoreHost: 5, scoreVisitor: 3),
            TestDataHelper.MakeGame(TeamA, TeamC, "Played", scoreHost: 5, scoreVisitor: 3),
            TestDataHelper.MakeGame(TeamB, TeamC, "Played", scoreHost: 5, scoreVisitor: 3),
        };

        var expected = new Standings(games).Keys.ToList();

        new Standings(games, RuleOf()).Keys.Should().Equal(expected);
        new Standings(games, RuleOf("Bogus", "AlsoNotReal")).Keys.Should().Equal(expected);
    }

    [Fact]
    public void ComparatorNames_AreCaseInsensitive()
    {
        var games = new List<Game>
        {
            TestDataHelper.MakeGame(TeamA, TeamC, "Played", scoreHost: 5, scoreVisitor: 0),
            TestDataHelper.MakeGame(TeamB, TeamD, "Played", scoreHost: 6, scoreVisitor: 0),
        };

        var standings = new Standings(games, RuleOf("wins", "runsscored"));

        standings.Keys.First().Should().Be(TeamB);
    }
}
