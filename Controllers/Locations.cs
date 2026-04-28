using Facet.Extensions;
using Microsoft.AspNetCore.Authorization;
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
                .Where(g => g.Status?.Name != "Played" && g.Status?.Name != "Deleted" && g.Date.Date >= today)
                .OrderBy(g => Math.Abs(g.Date.Subtract(now).TotalDays))
                .Take(5)
                .Select(g => new GameSummaryDto(g))
        }));
    }

    [Authorize(Policy = "Scope:Executive,Webmaster")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LocationUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.City))
            return BadRequest("Name and city are required.");

        var location = new Location
        {
            Name = dto.Name,
            FormalName = dto.FormalName,
            City = dto.City,
            Address = dto.Address,
            MapsPlaceID = dto.MapsPlaceID,
            Active = true
        };

        await dbContext.Locations.AddAsync(location);
        await dbContext.SaveChangesAsync();

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Locations", "Created location",
            new LocationDetailDto(location)));
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { }, new LocationDetailDto(location));
    }

    [Authorize(Policy = "Scope:Executive,Webmaster")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete([FromRoute] long id)
    {
        var location = await dbContext.Locations
            .Include(l => l.Games)
            .FirstOrDefaultAsync(l => l.ID == id);

        if (location is null) return NotFound();

        if (location.Games.Count > 0)
            return Conflict($"Cannot delete location — it has {location.Games.Count} game(s).");

        var snapshot = new LocationDetailDto(location);
        dbContext.Locations.Remove(location);
        await dbContext.SaveChangesAsync();

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Locations/" + id, "Deleted location",
            snapshot));
        await dbContext.SaveChangesAsync();

        return NoContent();
    }
}
