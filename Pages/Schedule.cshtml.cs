using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBackend.Pages;

public class ScheduleModel(LeagueSitesContext context) : PageModel
{

    public int Year { get; set; }
    public List<Game> Games { get; set; } = [];
    public List<Location?> Locations { get; set; } = [];
    public required PermissionsManager Permissions { get; set; }

    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync(int? year)
    {
        Permissions = await PermissionsManager.CreateAsync(User, dbContext);

        Year = year ?? DateTime.Now.Year;

        var seasonIDs = (await dbContext.Seasons
            .Where(s => s.Year == Year)
            .ToListAsync())
            .Select(s => s.ID);

        Games = await dbContext.Games
            .Where(g => g.Status!.Name != "Deleted")
            .Where(g => seasonIDs.Contains(g.SeasonID))
            .Include(g => g.Status)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Location)
            .Include(g => g.Season)
            .ToListAsync();
        
        // Code-side sort instead of database-side, because the date column in the database is just text
        // i.e. "5-10" sorts before "5-2" alphabetically but is not chronological
        Games = [.. Games.OrderBy(g => g.Date)];

        Locations = [.. Games.GroupBy(g => g.Location).Select(p => p.Key).OrderBy(p => p?.Name)];

        return Page();
    }
}
