using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class PlayoffsModel(LeagueSitesContext context) : PageModel
{
    public Season? CurrentSeason { get; set; }
    public Season? Playoffs { get; set; }
    
    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync(int? year)
    {
        if (year == null)
        {
            CurrentSeason = await IndexModel.GetClosestSeasonAsync(dbContext);
        }
        else
        {
            CurrentSeason = await dbContext.Seasons.FirstOrDefaultAsync(s => s.Year == (int)year && s.Subseason == "Playoffs");
            if (CurrentSeason == null)
            {
                return Redirect("/Playoffs");
            }
        }

        if (CurrentSeason == null)
        {
            return Redirect("/Index");
        }

        var SeedingSeason = await dbContext.Seasons.FirstOrDefaultAsync(s => s.Year == CurrentSeason.Year && s.Subseason == "Regular Season") ?? CurrentSeason;

        Playoffs = await dbContext.Seasons
            .Include(s => s.Tournaments)
                .ThenInclude(t => t.Brackets)
                    .ThenInclude(b => b.Rounds)
                        .ThenInclude(r => r.Series)
                            .ThenInclude(s => s.Games)
                                .ThenInclude(g => g.Game)
            .Include(s => s.Tournaments)
                .ThenInclude(t => t.RoundRobins)
                    .ThenInclude(r => r.Games)
                        .ThenInclude(g => g.Game)
            .Where(s => s.Year == CurrentSeason.Year && s.Subseason == "Playoffs")
            .FirstOrDefaultAsync();

        if (Playoffs != null)
        {
            var playoffGames = await dbContext.Games
                .Include(g => g.HostTeam)
                .Include(g => g.VisitingTeam)
                .Include(g => g.Status)
                .Include(g => g.Location)
                .Where(g => g.SeasonID == Playoffs.ID)
                .ToListAsync();
            
            var tournament = Playoffs.Tournaments.FirstOrDefault(); // For a Season of type "Playoffs", one (or zero) tournament(s) should exist. "First()" within the above if statement works based on this.
            tournament?.Populate(playoffGames, dbContext);
        }

        return Page();
    }
}