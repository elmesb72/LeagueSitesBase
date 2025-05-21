using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class StandingsModel(LeagueSitesContext context) : PageModel
{
    public Season? CurrentSeason { get; set; }
    public Standings? Standings { get; set; }

    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync(int? year)
    {
        if (year == null)
        {
            CurrentSeason = await IndexModel.GetClosestSeasonAsync(dbContext);
        }
        else
        {
            CurrentSeason = await dbContext.Seasons.FirstOrDefaultAsync(s => s.Year == (int)year && s.Subseason == "Regular Season");
            if (CurrentSeason == null)
            {
                return Redirect("/Standings");
            }
        }

        if (CurrentSeason == null)
        {
            return Redirect("/Index");
        }

        var seasonGames = await dbContext.Games
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Status)
            .Where(g => g.SeasonID == CurrentSeason.ID)
            .ToListAsync();
        Standings = new Standings(seasonGames);
        Standings.CalculateStreaks();
        return Page();
    }
}
