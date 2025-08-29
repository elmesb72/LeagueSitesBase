
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public class PermissionsManager
{
    List<PermissionsScope> SitePermissions { get; set; }
    Dictionary<Team, List<PermissionsScope>> TeamPermissions { get; set; }
    public User? User { get; private set; }

    PermissionsManager(List<PermissionsScope> sitePermissions, Dictionary<Team, List<PermissionsScope>> teamPermissions, User? user = null)
    {
        SitePermissions = sitePermissions;
        TeamPermissions = teamPermissions;
        User = user;
    }

    public static readonly PermissionsManager Public = new([PermissionsScope.Public], []);
    public static async Task<PermissionsManager> CreateAsync(ClaimsPrincipal user, LeagueSitesContext dbContext, List<Team>? teams = null)
    {
        if (user == null || user.Identity == null || !user.Identity.IsAuthenticated) return new([PermissionsScope.Public], []);

        var userID = Convert.ToInt64(user.Claims.First(c => c.Type == "UserID").Value);

        var siteUser = await dbContext.Users
            .Include(u => u.Invitations)
                .ThenInclude(i => i.InvitationEmails)
            .Include(u => u.Invitations)
                .ThenInclude(i => i.InvitationRoles)
                    .ThenInclude(r => r.Role)
            .Include(u => u.Invitations)
                .ThenInclude(i => i.Team)
            .Include(u => u.UserLogins)
                .ThenInclude(l => l.LoginSource)
            .Include(u => u.UserRoles)
                .ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.ID == userID);

        if (siteUser == null)
        {
            return new PermissionsManager([PermissionsScope.Public], []);
        }

        var sitePermissions = getSitePermissions(siteUser);
        var teamPermissions = getTeamPermissions(siteUser, teams);
        var username = siteUser.UserLogins.First(ul => ul.IsPrimary).Name;
        return new PermissionsManager(sitePermissions, teamPermissions, siteUser);
    }

    static List<PermissionsScope> getSitePermissions(User user)
    {
        List<PermissionsScope> permissions = [];
        foreach (UserRole userRole in user.UserRoles)
        {
            if (Enum.TryParse<PermissionsScope>(userRole.Role?.Name, out var permission))
            {
                permissions.Add(permission);
            }
        }
        return permissions;
    }

    static Dictionary<Team, List<PermissionsScope>> getTeamPermissions(User user, List<Team>? teams)
    {
        Dictionary<Team, List<PermissionsScope>> permissions = [];
        if (teams == null)
        {
            return permissions;
        }

        foreach (var team in teams)
        {
            if (user.Invitations.Any(i => i.Team == team))
            {
                permissions.Add(team, []);
                var invitation = user.Invitations.First(i => i.Team == team);
                foreach (var teamRole in invitation.InvitationRoles)
                {
                    if (Enum.TryParse<PermissionsScope>(teamRole.Role?.Name, out var permission))
                    {
                        permissions[team].Add(permission);
                    }
                }
            }
        }
        return permissions;
    }

    public bool Allow(string action)
    {
        List<PermissionsScope> postPermissions = [PermissionsScope.Reporter, PermissionsScope.Scorer, PermissionsScope.Manager, PermissionsScope.Executive, PermissionsScope.Webmaster];
        List<PermissionsScope> createGamePermissions = [PermissionsScope.Webmaster, PermissionsScope.Executive, PermissionsScope.Manager, PermissionsScope.Scorer];
        return action switch
        {
            "Post" => SitePermissions.Any(postPermissions.Contains) || TeamPermissions.Any(t => t.Value.Any(postPermissions.Contains)),
            "CreateGame" => SitePermissions.Any(createGamePermissions.Contains) || TeamPermissions.Any(t => t.Value.Any(createGamePermissions.Contains)),
            _ => false,
        };
    }

    public bool Include(List<PermissionsScope> scopes, Team? team = null)
    {
        if (SitePermissions.Any(scopes.Contains)) return true;
        if (team != null && TeamPermissions.TryGetValue(team, out List<PermissionsScope>? tp) && tp.Any(scopes.Contains)) return true;
        return false;
    }
}

public enum PermissionsScope
{
    Public,
    Reporter,
    Scorer,
    Manager,
    Executive,
    Webmaster,
}