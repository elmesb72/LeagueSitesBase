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
            .AsNoTracking()
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

        return Ok(locations.Select(l => new LocationDetailDto(l)));
    }
}
