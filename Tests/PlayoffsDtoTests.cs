using FluentAssertions;

namespace LeagueSitesBackend.Tests;

/// <summary>
/// The public Playoffs page shows a champion banner only when the backend says
/// the bracket is decided. That decision has to match the History page's
/// (every series has a winner), not "the finals have a winner", because a
/// third-place game can still be outstanding after the final — and it must
/// never name a winner from a half-played bracket.
/// </summary>
public class PlayoffsDtoTests
{
    static readonly Team Alphas = TestDataHelper.MakeTeam(1, "Alphas", "ALP");
    static readonly Team Betas = TestDataHelper.MakeTeam(2, "Betas", "BET");
    static readonly Team Gammas = TestDataHelper.MakeTeam(3, "Gammas", "GAM");
    static readonly Team Deltas = TestDataHelper.MakeTeam(4, "Deltas", "DEL");

    static RoundSeries MakeSeries(long number, Team? team1, Team? team2, Team? winner) => new()
    {
        ID = number,
        Number = number,
        Format = "Best of",
        HostOrder = "121",
        Matchup = "#1-#4",
        Spots = (new TournamentSeriesSpot('#', 1, team1), new TournamentSeriesSpot('#', 4, team2)),
        Winner = winner,
        Loser = winner is null ? null : (winner == team1 ? team2 : team1),
        Games = [],
    };

    /// <summary>Semi-finals decided (Deltas upset Alphas, Betas beat Gammas); final and 3rd-place game as given.</summary>
    static TournamentBracket MakeBracket(Team? finalWinner, Team? thirdPlaceWinner, bool historical = true) => new()
    {
        ID = 1,
        Name = "Main",
        Format = "Fixed",
        Historical = historical,
        Seeds = new Dictionary<int, Team> { [1] = Alphas, [2] = Betas, [3] = Gammas, [4] = Deltas },
        Rounds =
        [
            new BracketRound
            {
                ID = 1,
                Name = "Semi-finals",
                Series = [MakeSeries(1, Alphas, Deltas, Deltas), MakeSeries(2, Betas, Gammas, Betas)],
            },
            new BracketRound
            {
                ID = 2,
                Name = "Finals",
                Series =
                [
                    MakeSeries(3, Deltas, Betas, finalWinner),
                    MakeSeries(4, Alphas, Gammas, thirdPlaceWinner),
                ],
            },
        ],
    };

    [Fact]
    public void Undecided_bracket_has_no_winner()
    {
        var dto = BracketDto.From(MakeBracket(finalWinner: null, thirdPlaceWinner: null));

        dto.Winner.Should().BeNull();
        dto.Historical.Should().BeTrue();
        dto.Rounds.Should().HaveCount(2);
    }

    [Fact]
    public void A_finished_final_with_a_third_place_game_still_to_play_is_not_yet_decided()
    {
        // GetWinner alone would already answer "Deltas" here (two series wins),
        // but History would not yet show a champion, so neither do we.
        var dto = BracketDto.From(MakeBracket(finalWinner: Deltas, thirdPlaceWinner: null));

        dto.Winner.Should().BeNull();
    }

    [Fact]
    public void Fully_decided_bracket_names_the_team_with_the_most_series_wins()
    {
        var dto = BracketDto.From(MakeBracket(finalWinner: Deltas, thirdPlaceWinner: Alphas));

        dto.Winner.Should().NotBeNull();
        dto.Winner!.ID.Should().Be(Deltas.ID);
    }

    [Fact]
    public void Historical_flag_is_passed_through_for_non_championship_brackets()
    {
        var dto = BracketDto.From(MakeBracket(finalWinner: Deltas, thirdPlaceWinner: Alphas, historical: false));

        dto.Historical.Should().BeFalse();
        dto.Winner!.ID.Should().Be(Deltas.ID);
    }

    [Fact]
    public void Bracket_with_no_series_is_not_decided()
    {
        var bracket = new TournamentBracket { ID = 9, Name = "Empty", Format = "Fixed", Historical = true, Seeds = [] };

        BracketDto.From(bracket).Winner.Should().BeNull();
    }
}
