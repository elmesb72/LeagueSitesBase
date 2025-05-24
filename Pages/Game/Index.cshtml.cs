using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class GameModel(LeagueSitesContext context) : PageModel
{

    public Game? Game { get; set; }
    public Standings? Records { get; set; }
    public User? SiteUser { get; set; }
    public List<string> Permissions { get; set; } = [];

    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Game = await dbContext.Games
            .Include(g => g.Status)
            .Include(g => g.Location)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .FirstOrDefaultAsync(g => id == g.ID);

        if (Game == default) {
            return RedirectToPage("/Index");
        }

        var games = await dbContext.Games
            .Where(g => g.Status!.Name != "Deleted")
            .Where(g => g.SeasonID == Game.SeasonID && g.Date <= Game.Date)
            .Include(g => g.Status)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .OrderBy(g => g.Date)
            .ToListAsync();

        /* If no games exist on a date equal to or before this, the above set returns empty.
            An empty set of games means no there are no teams in the following Standings calculation.
            No teams in the standings means KeyNotFound exception displaying their 0-0 records. */
        if (!games.Any(g => g.ID == Game.ID)) {
            games.Add(Game);
        }
        Records = new Standings(games);

        SiteUser = await UserModel.GetSiteUser(User, dbContext);
        if (SiteUser != null)
        {
            Permissions = UserModel.GetUserPermissions(SiteUser, [Game.HostTeam, Game.VisitingTeam]);
        }

        return Page();
    }

}