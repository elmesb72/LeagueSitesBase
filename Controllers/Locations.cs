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
            .OrderBy(p => p.City)
            .ThenBy(p => p.Name)
            .SelectFacet<LocationDetailDto>()
            .ToListAsync();

        return Ok(locations);
    }
}
