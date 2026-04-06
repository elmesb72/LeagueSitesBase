using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Scorecard")]
public class APIScorecardController(LeagueSitesContext dbContext) : ControllerBase
{
    /// Returns batting events and lineup for each team given a game ID.
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get([FromRoute] long id)
    {
        var hostSide = new Scorecard
        {
            Lineup = await dbContext.BattingLineupEntries
                .Include(ble => ble.Player)
                .Where(ble => ble.GameID == id && ble.IsHostTeam)
                .OrderBy(ble => ble.Row)
                .ToListAsync(),
            Events = await dbContext.BattingEvents
                .Where(be => be.GameID == id && be.IsHostTeam)
                .OrderBy(be => be.Index)
                .ToListAsync()
        };

        var visitorSide = new Scorecard
        {
            Lineup = await dbContext.BattingLineupEntries
                .Where(ble => ble.GameID == id && !ble.IsHostTeam)
                .OrderBy(ble => ble.Row)
                .ToListAsync(),
            Events = await dbContext.BattingEvents
                .Where(be => be.GameID == id && !be.IsHostTeam)
                .OrderBy(be => be.Index)
                .ToListAsync()
        };

        return Ok(new { hostSide, visitorSide });
    }
}
