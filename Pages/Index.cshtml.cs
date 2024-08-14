using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class IndexModel(LeagueSitesContext context, IConfiguration config) : PageModel
{
    public required IEnumerable<Game> Games { get; set; }
    public required IEnumerable<News> News { get; set; }
    public required Standings Standings { get; set; }
    public bool IsPlayoffs { get; set; }

    readonly LeagueSitesContext _context = context;
    readonly IConfiguration _config = config;

    public async Task<IActionResult> OnGetAsync()
    {
        var startOfWeek = DateTime.Today.AddDays(-7);
        var endOfWeek = DateTime.Today.AddDays(7);

        Games = await _context.Games
            .Include(g => g.Season)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Location)
            .Include(g => g.Status)
            .Where(g => (g.Date >= startOfWeek && g.Date < endOfWeek && g.Status!.Name != "Cancelled" && g.Status.Name != "Deleted") || (g.Date <= DateTime.Now && g.Status!.Name == "Upcoming"))
            .OrderBy(g => g.Date)
            .ToListAsync();

        News = await _context.News
            .Where(n => !n.IsDeleted && !n.IsHidden)
            .OrderBy(n => n.Date)
            .ToListAsync();

        var closestSeason = await GetClosestSeasonAsync(_context);
        if (closestSeason is not null)
        {
            var seasonGames = await _context.Games
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Status)
            .Where(g => g.SeasonID == closestSeason.ID)
            .ToListAsync();
            Standings = new Standings(seasonGames);


            var playoffsSeason = await _context.Seasons
                .Include(s => s.Tournaments)
                .Where(s => s.Year == closestSeason.Year && s.Subseason == "Playoffs")
                .FirstOrDefaultAsync();
            if (playoffsSeason is not null)
            {
                IsPlayoffs = true;
            }
        }

        Standings ??= new Standings([]);

        return Page();
    }

    public static async Task<Season?> GetClosestSeasonAsync(LeagueSitesContext db)
    {
        var seasons = await db.Seasons.Where(s => s.Subseason == "Regular Season").ToListAsync();
        var closestSeason = seasons.FirstOrDefault(s => s.Subseason == "Regular Season");
        if (closestSeason is null)
        {
            return null;
        }
        foreach (var s in seasons)
        {
            if (DateTime.Today.Subtract(s.StartDate).Duration() < DateTime.Today.Subtract(closestSeason.StartDate).Duration())
            {
                closestSeason = s;
            }
        }
        return closestSeason;
    }
}