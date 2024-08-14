using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;
public class ExecutiveModel(LeagueSitesContext context) : PageModel
{
    readonly LeagueSitesContext dbContext = context;

    public List<Team> Teams { get; set; } = [];
    public List<Location> Locations { get; set; } = [];
    public Season? CurrentSeason { get; set; }
    public Season? CurrentPlayoffs { get; set; }

    public static bool IsAllowed(System.Security.Claims.ClaimsPrincipal user, LeagueSitesContext dbContext, out string? redirect)
    {
        var siteUser = UserModel.GetSiteUser(user, dbContext).Result;
        if (user.Identity is null || !user.Identity.IsAuthenticated || siteUser is null)
        {
            redirect = "/Login";
            return false;
        }
        var permissions = UserModel.GetUserPermissions(siteUser);
        if (!permissions.Any(p => new List<string>() { "Executive", "Webmaster" }.Contains(p)))
        {
            redirect = "/User";
            return false;
        }

        redirect = null;
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsAllowed(User, dbContext, out var redirect))
        {
            return RedirectToPage(redirect);
        }

        Teams = await dbContext.Teams.Where(t => !t.Hidden).OrderByDescending(t => t.Active).ThenBy(t => t.Name).ToListAsync();
        Locations = await dbContext.Locations.OrderByDescending(t => t.Active).ThenBy(t => t.Name).ToListAsync();
        CurrentSeason = await dbContext.Seasons
            .Where(s => s.Subseason == "Regular Season" && s.Year == DateTime.Now.Year)
            .Include(s => s.Games)
                .ThenInclude(g => g.Status)
            .Include(s => s.Tournaments)
                .ThenInclude(t => t.Brackets)
            .Include(s => s.Tournaments)
                .ThenInclude(t => t.RoundRobins)
            .FirstOrDefaultAsync();
        CurrentPlayoffs = await dbContext.Seasons
            .Where(s => s.Subseason == "Playoffs" && s.Year == DateTime.Now.Year)
            .Include(s => s.Tournaments)
                .ThenInclude(t => t.Brackets)
            .Include(s => s.Tournaments)
                .ThenInclude(t => t.RoundRobins)
            .FirstOrDefaultAsync();

        return Page();
    }
}
