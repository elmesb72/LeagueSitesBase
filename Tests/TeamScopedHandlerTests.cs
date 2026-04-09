using FluentAssertions;
using Moq;
using Moq.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace LeagueSitesBackend.Tests;

public class TeamScopedHandlerTests
{
    static readonly Team TeamA = TestDataHelper.MakeTeam(1, "Alphas", "ALP");
    static readonly Team TeamB = TestDataHelper.MakeTeam(2, "Betas", "BET");

    Mock<LeagueSitesContext> CreateDbMock(List<string> userRoles, Dictionary<int, string> teamRoles)
    {
        var dbMock = new Mock<LeagueSitesContext>();
        dbMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser(userRoles, teamRoles));
        dbMock.Setup(x => x.Teams).ReturnsDbSet(new List<Team> { TeamA, TeamB });
        return dbMock;
    }

    async Task<bool> RunHandler(
        Mock<LeagueSitesContext> dbMock,
        ITeamScoped resource,
        PermissionsScope[] requiredScopes,
        bool authenticated = true)
    {
        var permissionsService = new PermissionsService(dbMock.Object);
        var handler = new TeamScopedHandler(permissionsService, dbMock.Object);

        var user = authenticated
            ? TestDataHelper.GetFakeClaimsPrincipal(-1)
            : TestDataHelper.GetUnauthenticatedPrincipal();

        var requirement = new TeamScopedRequirement(requiredScopes);
        var context = new AuthorizationHandlerContext([requirement], user, resource);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task SiteWebmaster_CanEditAnyGame()
    {
        var dbMock = CreateDbMock(["Webmaster"], []);
        var game = new Game { HostTeam = TeamA, HostTeamID = 1, VisitingTeam = TeamB, VisitingTeamID = 2 };

        var result = await RunHandler(dbMock, game,
            [PermissionsScope.Manager, PermissionsScope.Scorer, PermissionsScope.Executive, PermissionsScope.Webmaster]);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TeamManager_CanEditOwnTeamGame()
    {
        var dbMock = CreateDbMock([], new() { [1] = "Manager" });
        var game = new Game { HostTeam = TeamA, HostTeamID = 1, VisitingTeam = TeamB, VisitingTeamID = 2 };

        var result = await RunHandler(dbMock, game,
            [PermissionsScope.Manager, PermissionsScope.Scorer, PermissionsScope.Executive, PermissionsScope.Webmaster]);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TeamManager_CannotEditUnrelatedGame()
    {
        var teamC = TestDataHelper.MakeTeam(3, "Gammas", "GAM");
        var dbMock = CreateDbMock([], new() { [3] = "Manager" });
        dbMock.Setup(x => x.Teams).ReturnsDbSet(new List<Team> { TeamA, TeamB, teamC });
        var game = new Game { HostTeam = TeamA, HostTeamID = 1, VisitingTeam = TeamB, VisitingTeamID = 2 };

        var result = await RunHandler(dbMock, game,
            [PermissionsScope.Manager, PermissionsScope.Scorer, PermissionsScope.Executive, PermissionsScope.Webmaster]);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UnauthenticatedUser_Denied()
    {
        var dbMock = CreateDbMock(["Webmaster"], []);
        var game = new Game { HostTeam = TeamA, HostTeamID = 1, VisitingTeam = TeamB, VisitingTeamID = 2 };

        var result = await RunHandler(dbMock, game,
            [PermissionsScope.Webmaster], authenticated: false);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TeamsNotLoaded_HydratedFromDb()
    {
        var dbMock = CreateDbMock([], new() { [1] = "Manager" });
        // Game has IDs but null navigation properties — handler should load them
        var game = new Game { HostTeam = null, HostTeamID = 1, VisitingTeam = null, VisitingTeamID = 2 };

        var result = await RunHandler(dbMock, game,
            [PermissionsScope.Manager, PermissionsScope.Scorer, PermissionsScope.Executive, PermissionsScope.Webmaster]);

        result.Should().BeTrue("handler should hydrate missing teams from DB");
    }

    [Fact]
    public async Task MixedLoadedTeams_HydratesMissingOnly()
    {
        var dbMock = CreateDbMock([], new() { [2] = "Scorer" });
        // Only host team loaded, visitor is null
        var game = new Game { HostTeam = TeamA, HostTeamID = 1, VisitingTeam = null, VisitingTeamID = 2 };

        var result = await RunHandler(dbMock, game,
            [PermissionsScope.Manager, PermissionsScope.Scorer, PermissionsScope.Executive, PermissionsScope.Webmaster]);

        result.Should().BeTrue("handler should load TeamB and find Scorer role");
    }

    [Fact]
    public async Task Reporter_DeniedForGameEdit()
    {
        var dbMock = CreateDbMock([], new() { [1] = "Reporter" });
        var game = new Game { HostTeam = TeamA, HostTeamID = 1, VisitingTeam = TeamB, VisitingTeamID = 2 };

        var result = await RunHandler(dbMock, game,
            [PermissionsScope.Manager, PermissionsScope.Scorer, PermissionsScope.Executive, PermissionsScope.Webmaster]);

        result.Should().BeFalse("Reporter is not in the required scopes for game editing");
    }
}
