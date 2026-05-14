using Moq;
using Moq.EntityFrameworkCore;
using FluentAssertions;
using System.Security.Claims;

namespace LeagueSitesBackend.Tests;

public class PermissionsManagerTests
{
    [Fact]
    public async Task PublicUser_HasOnlyPublicScope()
    {
        var dbMock = new Mock<LeagueSitesContext>();
        dbMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser([], []));

        var permissions = await PermissionsManager.CreateAsync(null, dbMock.Object);

        permissions.Include([PermissionsScope.Public]).Should().BeTrue();
        permissions.Include([PermissionsScope.Webmaster]).Should().BeFalse();
        permissions.Include([PermissionsScope.Executive]).Should().BeFalse();
        permissions.Include([PermissionsScope.Manager]).Should().BeFalse();
        permissions.Allow("Post").Should().BeFalse();
        permissions.Allow("CreateGame").Should().BeFalse();
        permissions.User.Should().BeNull();
    }

    [Fact]
    public async Task UnauthenticatedPrincipal_HasOnlyPublicScope()
    {
        var user = TestDataHelper.GetUnauthenticatedPrincipal();
        var dbMock = new Mock<LeagueSitesContext>();
        dbMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser([], []));

        var permissions = await PermissionsManager.CreateAsync(user, dbMock.Object);

        permissions.Include([PermissionsScope.Public]).Should().BeTrue();
        permissions.User.Should().BeNull();
    }

    [Fact]
    public async Task Webmaster_HasSiteLevelAccess()
    {
        var user = TestDataHelper.GetFakeClaimsPrincipal(-1);
        var dbMock = new Mock<LeagueSitesContext>();
        dbMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser(["Webmaster"], []));

        var permissions = await PermissionsManager.CreateAsync(user, dbMock.Object);

        permissions.Include([PermissionsScope.Webmaster]).Should().BeTrue();
        permissions.Include([PermissionsScope.Executive]).Should().BeFalse();
        permissions.Include([PermissionsScope.Public]).Should().BeFalse();
        permissions.Allow("Post").Should().BeTrue();
        permissions.Allow("CreateGame").Should().BeTrue();
        permissions.User.Should().NotBeNull();
    }

    [Fact]
    public async Task Executive_HasSiteLevelAccess()
    {
        var user = TestDataHelper.GetFakeClaimsPrincipal(-1);
        var dbMock = new Mock<LeagueSitesContext>();
        dbMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser(["Executive"], []));

        var permissions = await PermissionsManager.CreateAsync(user, dbMock.Object);

        permissions.Include([PermissionsScope.Executive]).Should().BeTrue();
        permissions.Include([PermissionsScope.Webmaster]).Should().BeFalse();
        permissions.Allow("Post").Should().BeTrue();
        permissions.Allow("CreateGame").Should().BeTrue();
    }

    [Fact]
    public async Task Manager_HasTeamScopedAccess()
    {
        var user = TestDataHelper.GetFakeClaimsPrincipal(-1);
        var team = TestDataHelper.MakeTeam(-1);
        var otherTeam = TestDataHelper.MakeTeam(-2, "Other");
        var dbMock = new Mock<LeagueSitesContext>();
        dbMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser([], new() { [-1] = "Manager" }));

        var permissions = await PermissionsManager.CreateAsync(user, dbMock.Object, [team]);

        permissions.Include([PermissionsScope.Manager], team).Should().BeTrue();
        permissions.Include([PermissionsScope.Manager], otherTeam).Should().BeFalse();
        permissions.Include([PermissionsScope.Manager]).Should().BeFalse("site-level check should not match team-only role");
        permissions.Include([PermissionsScope.Webmaster]).Should().BeFalse();
        permissions.Allow("Post").Should().BeTrue();
        permissions.Allow("CreateGame").Should().BeTrue();
    }

    [Fact]
    public async Task Scorer_HasTeamScopedAccess()
    {
        var user = TestDataHelper.GetFakeClaimsPrincipal(-1);
        var team = TestDataHelper.MakeTeam(-1);
        var dbMock = new Mock<LeagueSitesContext>();
        dbMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser([], new() { [-1] = "Scorer" }));

        var permissions = await PermissionsManager.CreateAsync(user, dbMock.Object, [team]);

        permissions.Include([PermissionsScope.Scorer], team).Should().BeTrue();
        permissions.Include([PermissionsScope.Manager], team).Should().BeFalse();
        permissions.Allow("Post").Should().BeTrue();
        permissions.Allow("CreateGame").Should().BeTrue();
    }

    [Fact]
    public async Task Reporter_CanPostButNotCreateGame()
    {
        var user = TestDataHelper.GetFakeClaimsPrincipal(-1);
        var team = TestDataHelper.MakeTeam(-1);
        var dbMock = new Mock<LeagueSitesContext>();
        dbMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser([], new() { [-1] = "Reporter" }));

        var permissions = await PermissionsManager.CreateAsync(user, dbMock.Object, [team]);

        permissions.Allow("Post").Should().BeTrue();
        permissions.Allow("CreateGame").Should().BeFalse();
    }

    [Fact]
    public async Task UnknownAction_ReturnsFalse()
    {
        var user = TestDataHelper.GetFakeClaimsPrincipal(-1);
        var dbMock = new Mock<LeagueSitesContext>();
        dbMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser(["Webmaster"], []));

        var permissions = await PermissionsManager.CreateAsync(user, dbMock.Object);

        permissions.Allow("NonexistentAction").Should().BeFalse();
    }

    [Fact]
    public async Task UserNotInDatabase_GetsPublicScope()
    {
        var user = TestDataHelper.GetFakeClaimsPrincipal(-999);
        var dbMock = new Mock<LeagueSitesContext>();
        dbMock.Setup(x => x.Users).ReturnsDbSet(new List<User>());

        var permissions = await PermissionsManager.CreateAsync(user, dbMock.Object);

        permissions.Include([PermissionsScope.Public]).Should().BeTrue();
        permissions.User.Should().BeNull();
    }

    [Fact]
    public async Task TeamNotPassedToCreateAsync_DefaultsToAllInvitationTeams()
    {
        var user = TestDataHelper.GetFakeClaimsPrincipal(-1);
        var team = TestDataHelper.MakeTeam(-1);
        var dbMock = new Mock<LeagueSitesContext>();
        dbMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser([], new() { [-1] = "Manager" }));

        // Don't pass teams — should default to every team the user has an invitation to,
        // so gatekeeper-style checks (IncludeAnyScope, Allow) still see team-scoped roles.
        var permissions = await PermissionsManager.CreateAsync(user, dbMock.Object);

        permissions.Include([PermissionsScope.Manager], team).Should().BeTrue();
        permissions.IncludeAnyScope([PermissionsScope.Manager]).Should().BeTrue();
        permissions.Allow("Post").Should().BeTrue();
        permissions.Allow("CreateGame").Should().BeTrue();
    }
}
