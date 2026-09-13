using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.EntityFrameworkCore;

namespace LeagueSitesBackend.Tests;

/// <summary>
/// The public Playoffs page offers executives an edit link for whichever year it
/// is showing. It needs the season's tournament id, and it must pick the same
/// tournament the public page renders: the season's first one.
/// </summary>
public class TournamentForSeasonTests
{
    static APITournamentController Controller(params Tournament[] tournaments)
    {
        var db = new Mock<LeagueSitesContext>();
        db.Setup(x => x.Tournaments).ReturnsDbSet(tournaments.ToList());
        return new APITournamentController(db.Object, Mock.Of<ITournamentService>());
    }

    [Fact]
    public async Task Returns_the_first_tournament_of_the_season()
    {
        var controller = Controller(
            new Tournament { ID = 7, SeasonID = 14 },
            new Tournament { ID = 3, SeasonID = 14 },
            new Tournament { ID = 9, SeasonID = 15 });

        var result = await controller.ForSeason(14);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { id = 3L });
    }

    [Fact]
    public async Task Season_without_a_tournament_is_not_found()
    {
        var controller = Controller(new Tournament { ID = 9, SeasonID = 15 });

        var result = await controller.ForSeason(14);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
