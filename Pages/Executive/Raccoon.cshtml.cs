using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBackend.Pages;

public class ExecutiveDeletedGamesModel(LeagueSitesContext context) : PageModel
{
    readonly LeagueSitesContext dbContext = context;

    public List<Game> DeletedGames { get; set; } = [];

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

        DeletedGames = await dbContext.Games
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Location)
            .Include(g => g.Status)
            .Include(g => g.SeriesGames)
            .Include(g => g.RoundRobinGames)
            .Where(g => g.Status!.Name == "Deleted")
            .OrderByDescending(g => g.Date)
            .ToListAsync();

        return Page();
    }
}