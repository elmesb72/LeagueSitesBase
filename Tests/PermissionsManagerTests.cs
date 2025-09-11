using Moq;
using Moq.EntityFrameworkCore;
using FluentAssertions;
using System.Security.Claims;

namespace LeagueSitesBase.Tests;

public class PermissionsManagerTests
{
    [Fact]
    public async Task TestIncludeAndAllowForPublicUser()
    {
        // Arrange
        ClaimsPrincipal? user = null;
        var team = new Team() { Location = "", Name = "", Abbreviation = "", BackgroundColor = "", Color = "", ID = -1 };
        var usersContextMock = new Mock<LeagueSitesContext>();
        usersContextMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser(["Webmaster"], []));

        // Act
        var permissions = await PermissionsManager.CreateAsync(user, usersContextMock.Object);

        // Assert
        permissions.Include([PermissionsScope.Public]).Should().BeTrue();
        permissions.Include([PermissionsScope.Scorer, PermissionsScope.Manager, PermissionsScope.Reporter], null).Should().BeFalse();
        permissions.Include([PermissionsScope.Scorer, PermissionsScope.Manager, PermissionsScope.Reporter], team).Should().BeFalse();
        permissions.Include([PermissionsScope.Executive, PermissionsScope.Webmaster]).Should().BeFalse();
        permissions.Allow("FakeAction").Should().BeFalse();
        permissions.Allow("Post").Should().BeFalse();
        permissions.Allow("CreateGame").Should().BeFalse();
    }

    [Fact]
    public async Task TestIncludeAndAllowForWebmaster()
    {
        // Arrange
        var user = TestDataHelper.GetFakeClaimsPrincipal(-1);
        var team = new Team() { Location = "", Name = "", Abbreviation = "", BackgroundColor = "", Color = "", ID = -1 };
        var usersContextMock = new Mock<LeagueSitesContext>();
        usersContextMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser(["Webmaster"], []));

        // Act
        var permissions = await PermissionsManager.CreateAsync(user, usersContextMock.Object);

        // Assert
        permissions.Include([PermissionsScope.Public]).Should().BeFalse();
        permissions.Include([PermissionsScope.Scorer, PermissionsScope.Manager, PermissionsScope.Reporter], null).Should().BeFalse();
        permissions.Include([PermissionsScope.Scorer, PermissionsScope.Manager, PermissionsScope.Reporter], team).Should().BeFalse();
        permissions.Include([PermissionsScope.Executive]).Should().BeFalse();
        permissions.Include([PermissionsScope.Webmaster]).Should().BeTrue();
        permissions.Allow("Post").Should().BeTrue();
        permissions.Allow("CreateGame").Should().BeTrue();
    }

    [Fact]
    public async Task TestIncludeAndAllowForManager()
    {
        // Arrange
        var user = TestDataHelper.GetFakeClaimsPrincipal(-1);
        var team = new Team() { Location = "", Name = "", Abbreviation = "", BackgroundColor = "", Color = "", ID = -1 };
        var usersContextMock = new Mock<LeagueSitesContext>();
        usersContextMock.Setup(x => x.Users).ReturnsDbSet(TestDataHelper.GetFakeUser([], new() { [-1] = "Manager" }));

        // Act
        var permissions = await PermissionsManager.CreateAsync(user, usersContextMock.Object, [team]);

        // Assert
        permissions.Include([PermissionsScope.Public]).Should().BeFalse();
        permissions.Include([PermissionsScope.Scorer, PermissionsScope.Manager, PermissionsScope.Reporter], null).Should().BeFalse();
        permissions.Include([PermissionsScope.Scorer, PermissionsScope.Manager, PermissionsScope.Reporter], team).Should().BeTrue();
        permissions.Include([PermissionsScope.Executive]).Should().BeFalse();
        permissions.Include([PermissionsScope.Webmaster]).Should().BeFalse();
        permissions.Allow("Post").Should().BeTrue();
        permissions.Allow("CreateGame").Should().BeTrue();
    }
}
