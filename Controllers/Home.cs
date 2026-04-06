using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Home")]
public class APIHomeController(LeagueSitesContext dbContext, IConfiguration config, ISeasonService seasonService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var startOfWeek = DateTime.Today.AddDays(-7);
        var endOfWeek = DateTime.Today.AddDays(7);

        var games = await dbContext.Games
            .AsNoTracking()
            .Include(g => g.Season)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Location)
            .Include(g => g.Status)
            .Where(g =>
                (g.Date >= startOfWeek && g.Date < endOfWeek
                    && g.Status!.Name != "Cancelled" && g.Status.Name != "Deleted")
                || (g.Date <= DateTime.Now && g.Status!.Name == "Upcoming"))
            .OrderBy(g => g.Date)
            .ToListAsync();

        var news = await GetFilteredNews();

        var closestSeason = await seasonService.GetClosestSeasonAsync();
        Standings? standings = null;
        bool isPlayoffs = false;

        if (closestSeason is not null)
        {
            var seasonGames = await dbContext.Games
                .AsNoTracking()
                .Include(g => g.HostTeam)
                .Include(g => g.VisitingTeam)
                .Include(g => g.Status)
                .Where(g => g.SeasonID == closestSeason.ID)
                .ToListAsync();
            standings = new Standings(seasonGames);

            isPlayoffs = await dbContext.Seasons
                .AnyAsync(s => s.Year == closestSeason.Year && s.Subseason == "Playoffs");
        }

        return Ok(new { games, news, standings, isPlayoffs });
    }

    async Task<List<News>> GetFilteredNews()
    {
        var allNews = await dbContext.News
            .AsNoTracking()
            .Include(n => n.Author)
                .ThenInclude(u => u!.UserLogins)
            .Include(n => n.Author)
                .ThenInclude(u => u!.Invitations)
                    .ThenInclude(i => i.Team)
            .Where(n => !n.IsDeleted && !n.IsHidden)
            .OrderByDescending(n => n.Date)
            .ToListAsync();

        IEnumerable<News> filtered = allNews;

        if (int.TryParse(config["Site:Home:NewsMaxAgeDays"], out var maxAge)
            && int.TryParse(config["Site:Home:NewsMinItems"], out var minItems))
        {
            var recent = allNews
                .Where(n => DateTime.Compare(n.Date, DateTime.Now.AddDays(-maxAge)) >= 0)
                .ToList();
            filtered = recent.Count < minItems
                ? allNews.Take(minItems)
                : recent;
        }

        // Include hidden news authored by the current user
        if (User.Identity?.IsAuthenticated == true)
        {
            var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
            var hiddenAuthored = await dbContext.News
                .AsNoTracking()
                .Include(n => n.Author)
                    .ThenInclude(u => u!.UserLogins)
                .Include(n => n.Author)
                    .ThenInclude(u => u!.Invitations)
                        .ThenInclude(i => i.Team)
                .Where(n => n.IsHidden && !n.IsDeleted && n.AuthorID == uid)
                .ToListAsync();
            filtered = filtered.Concat(hiddenAuthored).OrderByDescending(n => n.Date);
        }

        return filtered.ToList();
    }
}
