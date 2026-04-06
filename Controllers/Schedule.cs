using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Schedule")]
public class APIScheduleController(LeagueSitesContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? year)
    {
        var targetYear = year ?? DateTime.Now.Year;

        var seasonIDs = await dbContext.Seasons
            .Where(s => s.Year == targetYear)
            .Select(s => s.ID)
            .ToListAsync();

        var games = await dbContext.Games
            .Where(g => g.Status!.Name != "Deleted")
            .Where(g => seasonIDs.Contains(g.SeasonID))
            .Include(g => g.Status)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Location)
            .Include(g => g.Season)
            .ToListAsync();

        // Code-side sort because the date column in SQLite is text
        games = [.. games.OrderBy(g => g.Date)];

        var locations = games
            .GroupBy(g => g.Location)
            .Select(p => p.Key)
            .OrderBy(p => p?.Name)
            .ToList();

        return Ok(new { year = targetYear, games, locations });
    }
}
