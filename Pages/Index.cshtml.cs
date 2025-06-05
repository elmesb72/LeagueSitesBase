using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class IndexModel(LeagueSitesContext context, IConfiguration config) : PageModel
{
    public required IEnumerable<Game> Games { get; set; }
    public required IEnumerable<News> News { get; set; }
    public required Standings Standings { get; set; }
    public required PermissionsManager Permissions { get; set; }
    public bool IsPlayoffs { get; set; }

    readonly LeagueSitesContext dbContext = context;
    readonly IConfiguration config = config;

    public async Task<IActionResult> OnGetAsync()
    {
        Permissions = await PermissionsManager.CreateAsync(User, dbContext);

        var startOfWeek = DateTime.Today.AddDays(-7);
        var endOfWeek = DateTime.Today.AddDays(7);

        Games = await dbContext.Games
            .Include(g => g.Season)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Location)
            .Include(g => g.Status)
            .Where(g => (g.Date >= startOfWeek && g.Date < endOfWeek && g.Status!.Name != "Cancelled" && g.Status.Name != "Deleted") || (g.Date <= DateTime.Now && g.Status!.Name == "Upcoming"))
            .OrderBy(g => g.Date)
            .ToListAsync();

        News = await dbContext.News
            .Include(n => n.Author)
                .ThenInclude(u => u!.UserLogins)
            .Include(n => n.Author)
                .ThenInclude(u => u!.Invitations)
                    .ThenInclude(i => i.Team)
            .Where(n => !n.IsDeleted && !n.IsHidden)
            .OrderByDescending(n => n.Date)
            .ToListAsync();

        // Filter out old posts based on site config
        if (int.TryParse(config["Site:Home:NewsMaxAgeDays"], out var newsMaxAgeDays) && int.TryParse(config["Site:Home:NewsMinItems"], out var newsMinItems))
        {
            var recentNews = News.Where(n => DateTime.Compare(n.Date, DateTime.Now.AddDays(-newsMaxAgeDays)) >= 0).ToList();
            if (recentNews.Count < newsMinItems)
            {
                News = News.Take(newsMinItems);
            }
            else
            {
                News = recentNews;
            }
        }

        // Include hidden news authored by the current user (regardless of age)
        if (Permissions.User != null)
        {
            var hiddenAuthoredNews = await dbContext.News
            .Include(n => n.Author)
                .ThenInclude(u => u!.UserLogins)
            .Include(n => n.Author)
                .ThenInclude(u => u!.Invitations)
                    .ThenInclude(i => i.Team)
            .Where(n => n.IsHidden && n.AuthorID == Permissions.User!.ID)
            .ToListAsync();
            News = News.Concat(hiddenAuthoredNews).OrderByDescending(n => n.Date);
        }

        var closestSeason = await GetClosestSeasonAsync(dbContext);
        if (closestSeason is not null)
        {
            var seasonGames = await dbContext.Games
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Status)
            .Where(g => g.SeasonID == closestSeason.ID)
            .ToListAsync();
            Standings = new Standings(seasonGames);


            var playoffsSeason = await dbContext.Seasons
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