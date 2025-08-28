using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class ScoresModel(LeagueSitesContext context) : PageModel
{
    public DateTime Date { get; set; }
    public List<Game> Games { get; set; } = [];
    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync(string? day)
    {
        if (day == null)
        {
            return RedirectToPage("/Scores", new { day = DateTime.Today.ToString("yyyy-MM-dd") });
        }

        if (!DateTime.TryParse(day, out var parsedDate))
        {
            return RedirectToPage("/Scores", new { day = DateTime.Today.ToString("yyyy-MM-dd") });
        }
        Date = parsedDate;
        Games = await GetGames(parsedDate);

        return Page();
    }

    public async Task<List<Game>> GetGames(DateTime date)
    {
        List<string> excludedStatuses = ["Cancelled", "Deleted"];
        var games = await dbContext.Games
            .Include(g => g.Status)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Location)
            .Where(g => g.Date.Date == date.Date && !excludedStatuses.Contains(g.Status!.Name))
            .ToListAsync();
        var currentSeason = games.FirstOrDefault()?.SeasonID;
        if (currentSeason != null)
        {
            var standingsToDate = new Standings(await dbContext.Games
                .Include(g => g.Status)
                .Include(g => g.HostTeam)
                .Include(g => g.VisitingTeam)
                .Where(g => g.Status!.Name == "Played" && g.SeasonID == currentSeason && g.Date.Date <= date.Date)
                .ToListAsync()
            );
            games.ForEach(g => g.Standings = standingsToDate);
        }
        return games;
    }
}