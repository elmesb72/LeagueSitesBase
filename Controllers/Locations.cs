using Facet.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Locations")]
public class APILocationsController(LeagueSitesContext dbContext) : ControllerBase
{
    [ResponseCache(Duration = 30)]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var locations = await dbContext.Locations
            .Where(p => p.Active)
            .Include(p => p.Games)
                .ThenInclude(g => g.Status)
            .Include(p => p.Games)
                .ThenInclude(g => g.HostTeam)
            .Include(p => p.Games)
                .ThenInclude(g => g.VisitingTeam)
            .OrderBy(p => p.City)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var now = DateTime.Now;
        var today = DateTime.Today;

        return Ok(locations.Select(l => new
        {
            id = l.ID,
            name = l.Name,
            formalName = l.FormalName,
            city = l.City,
            address = l.Address,
            mapsPlaceId = l.MapsPlaceID,
            recentGames = l.Games
                .Where(g => g.Status?.Name == "Played")
                .OrderBy(g => Math.Abs(g.Date.Subtract(now).TotalDays))
                .Take(5)
                .Select(g => new GameSummaryDto(g)),
            upcomingGames = l.Games
                .Where(g => g.Status?.Name != "Played" && g.Date.Date >= today)
                .OrderBy(g => Math.Abs(g.Date.Subtract(now).TotalDays))
                .Take(5)
                .Select(g => new GameSummaryDto(g))
        }));
    }
}
