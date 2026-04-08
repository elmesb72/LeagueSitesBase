using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LeagueSitesBase.Tests;

public static class TestDataHelper
{
    public static Team MakeTeam(long id, string name = "Team", string abbreviation = "TM") => new()
    {
        ID = id,
        Location = $"{name} City",
        Name = name,
        Abbreviation = abbreviation,
        BackgroundColor = "FFFFFF",
        Color = "000000",
    };

    public static Game MakeGame(
        Team host, Team visitor,
        string status = "Played",
        long? scoreHost = null, long? scoreVisitor = null,
        DateTime? date = null) => new()
    {
        HostTeam = host,
        HostTeamID = host.ID,
        VisitingTeam = visitor,
        VisitingTeamID = visitor.ID,
        Status = new GameStatus { Name = status },
        ScoreHost = scoreHost,
        ScoreVisitor = scoreVisitor,
        Date = date ?? DateTime.Today,
    };

    public static List<User> GetFakeUser(List<string> userRoleNames, Dictionary<int, string> invitationRoleTeamIDNames)
    {
        return [
            new()
            {
                ID = -1,
                Invitations = [.. invitationRoleTeamIDNames.Select(irtn =>
                    new Invitation()
                    {
                        Team = MakeTeam(irtn.Key),
                        TeamID = irtn.Key,
                        InvitationEmails = [new InvitationEmail() { Email = "fake@email.address" }],
                        InvitationRoles = [new InvitationRole() { Role = new Role() { Name = irtn.Value } }],
                    }
                )],
                UserLogins = [
                    new UserLogin()
                    {
                        Name = "Fake User",
                        Email = "fake@email.address",
                        IsPrimary = true,
                        LoginSource = new UserLoginSource() { Source = "FakeSource" },
                    }
                ],
                UserRoles = [.. userRoleNames.Select(urn =>
                    new UserRole() { Role = new Role() { Name = urn } }
                )],
            }
        ];
    }

    public static ClaimsPrincipal GetFakeClaimsPrincipal(int userId)
    {
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim("UserID", userId.ToString()));
        return new ClaimsPrincipal(identity);
    }

    public static ClaimsPrincipal GetUnauthenticatedPrincipal() => new(new ClaimsIdentity());
}
