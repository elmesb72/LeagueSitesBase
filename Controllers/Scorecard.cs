using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Scorecard")]
public class APIScorecardController(LeagueSitesContext dbContext) : ControllerBase
{
    /// Returns batting events and lineup for each team given a game ID.
    [ResponseCache(Duration = 30)]
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get([FromRoute] long id)
    {
        var hostSide = new Scorecard
        {
            Lineup = await dbContext.BattingLineupEntries
                .AsNoTracking()
                .Include(ble => ble.Player)
                .Where(ble => ble.GameID == id && ble.IsHostTeam)
                .OrderBy(ble => ble.Row)
                .ToListAsync(),
            Events = await dbContext.BattingEvents
                .AsNoTracking()
                .Where(be => be.GameID == id && be.IsHostTeam)
                .OrderBy(be => be.Index)
                .ToListAsync()
        };

        var visitorSide = new Scorecard
        {
            Lineup = await dbContext.BattingLineupEntries
                .AsNoTracking()
                .Where(ble => ble.GameID == id && !ble.IsHostTeam)
                .OrderBy(ble => ble.Row)
                .ToListAsync(),
            Events = await dbContext.BattingEvents
                .AsNoTracking()
                .Where(be => be.GameID == id && !be.IsHostTeam)
                .OrderBy(be => be.Index)
                .ToListAsync()
        };

        return Ok(new { hostSide, visitorSide });
    }
}
