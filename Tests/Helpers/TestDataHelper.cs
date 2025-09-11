using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LeagueSitesBase.Tests;

public static class TestDataHelper
{
    public static List<User> GetFakeUser(List<string> userRoleNames, Dictionary<int, string> invitationRoleTeamIDNames)
    {  
        return [
            new()
            {
                ID = -1,
                Invitations = [.. invitationRoleTeamIDNames.Select(irtn => 
                    new Invitation() {
                        Team = new Team()
                        {
                            ID = irtn.Key,
                            Location = "Fake Place",
                            Name = "Fake Team",
                            Abbreviation = "FAKE",
                            BackgroundColor = "FFFFFF",
                            Color = "000000",
                        },
                        InvitationEmails = [
                            new InvitationEmail()
                            {
                                Email = "fake@email.address",
                            }
                        ],
                        InvitationRoles = [
                            new InvitationRole()
                            {
                                Role = new Role()
                                {
                                    Name = irtn.Value
                                }
                            }
                        ] // InvitationRoles must include Role
                    }
                )],
                UserLogins = [
                    new UserLogin()
                    {
                        Name = "Fake User",
                        Email = "fake@email.address",
                        IsPrimary = true,
                        LoginSource = new UserLoginSource()
                        {
                            Source = "FakeSource",
                        },
                    }
                ], // UserLogins must include LoginSource
                UserRoles = [.. userRoleNames.Select(urn => 
                    new UserRole()
                    {
                        Role = new Role()
                        {
                            Name = urn
                        }
                    }
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
}