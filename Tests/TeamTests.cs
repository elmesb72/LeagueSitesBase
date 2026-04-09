using FluentAssertions;

namespace LeagueSitesBackend.Tests;

public class TeamTests
{
    [Fact]
    public void TestTeamEquality()
    {
        // Arrange
        var teamA = new Team()
        {
            ID = 1,
            Name = "A",
            Location = "A",
            Abbreviation = "AA",
            BackgroundColor = "AAAAAA",
            Color = "AAAAAA",
        };
        var teamB = new Team()
        {
            ID = 1,
            Name = "B",
            Location = "B",
            Abbreviation = "BB",
            BackgroundColor = "BBBBBB",
            Color = "BBBBBB",
        };

        // Act
        var teamsAreEqual = teamA == teamB;

        // Assert
        teamsAreEqual.Should().BeTrue();
    }
}