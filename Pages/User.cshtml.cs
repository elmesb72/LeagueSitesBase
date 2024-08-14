using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class UserModel(LeagueSitesContext context) : PageModel
{
    public required string CallbackUrlBase { get; set; }
    public required string Error { get; set; }

    public User? SiteUser { get; set; }

    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity is null || !User.Identity.IsAuthenticated)
        {
            return RedirectToPage("/Login");
        }

        CallbackUrlBase = "https://" + Request.Host.Value + "/OAuth";

        var errorExists = Request.Query.TryGetValue("error", out var error);
        if (errorExists)
        {
            Error = error!;
        }

        SiteUser = await GetSiteUser(User, dbContext);

        return Page();
    }

    public static async Task<User?> GetSiteUser(System.Security.Claims.ClaimsPrincipal user, LeagueSitesContext dbContext)
    {
        if (user.Identity is not null && !user.Identity.IsAuthenticated)
        {
            return null;
        }
        else
        {
            var uid = Convert.ToInt64(user.Claims.First(c => c.Type == "UserID").Value);
            return await dbContext.Users
                .Include(u => u.Invitations)
                    .ThenInclude(i => i.InvitationEmails)
                .Include(u => u.Invitations)
                    .ThenInclude(i => i.InvitationRoles)
                        .ThenInclude(r => r.Role)
                .Include(u => u.Invitations)
                    .ThenInclude(i => i.Player)
                .Include(u => u.Invitations)
                        .ThenInclude(p => p.Status)
                .Include(u => u.Invitations)
                    .ThenInclude(i => i.Team)
                .Include(u => u.UserLogins)
                    .ThenInclude(l => l.LoginSource)
                .Include(u => u.UserRoles)
                    .ThenInclude(r => r.Role)
                .FirstOrDefaultAsync(u => u.ID == uid);
        }
    }

    public static List<string> GetUserPermissions(User user, List<Team>? teams = null)
    {
        if (user is null)
        {
            return [];
        }
        var permissions = new List<string>
        {
            "User"
        };
        permissions.AddRange(user.UserRoles.Select(ur => ur.Role?.Name ?? "Unknown"));
        permissions.Add(user.UserLogins.First(ul => ul.IsPrimary).Name);

        if (teams is not null)
        {
            foreach (var team in teams)
            {
                if (user.Invitations.Any(i => i.Team == team))
                {
                    permissions.Add(team.Abbreviation);
                    permissions.AddRange(user.Invitations.First(i => i.Team == team).InvitationRoles.Select(ir => team.Abbreviation + "-" + (ir.Role?.Name ?? "Unknown")));
                }
            }
        }

        return permissions;
    }
}