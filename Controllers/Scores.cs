using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Scores")]
public class APIScoresController(LeagueSitesContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? day)
    {
        if (!DateTime.TryParse(day, out var date))
            date = DateTime.Today;

        List<string> excludedStatuses = ["Cancelled", "Deleted"];

        var games = await dbContext.Games
            .AsNoTracking()
            .Include(g => g.Status)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Location)
            .Where(g => g.Date.Date == date.Date && !excludedStatuses.Contains(g.Status!.Name))
            .ToListAsync();

        Standings? standings = null;
        var currentSeason = games.FirstOrDefault()?.SeasonID;
        if (currentSeason != null)
        {
            var standingsGames = await dbContext.Games
                .AsNoTracking()
                .Include(g => g.Status)
                .Include(g => g.HostTeam)
                .Include(g => g.VisitingTeam)
                .Where(g => g.Status!.Name == "Played"
                    && g.SeasonID == currentSeason
                    && g.Date.Date <= date.Date)
                .ToListAsync();
            standings = new Standings(standingsGames);
        }

        return Ok(new { date, games, standings });
    }
}
