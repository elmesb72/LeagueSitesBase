using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api")]
public class APIScorecardController : ControllerBase
{
    public readonly LeagueSitesContext dbContext;
    public APIScorecardController(LeagueSitesContext context) => dbContext = context;

    /// Returns a list of batting events and lineup for each team given a game ID.
    [HttpGet("Scorecard/{id}")]
    public async Task<IActionResult> OnGetAsync([FromRoute] long id)
    {
        // For lineups, client side secondary sort should still be done on Out (or Position for offensive replacements)
        var hostSide = new Scorecard()
        {
            Lineup = await dbContext.BattingLineupEntries.Include(ble => ble.Player).Where(ble => ble.GameID == id && ble.IsHostTeam).OrderBy(ble => ble.Row).ToListAsync(),
            Events = await dbContext.BattingEvents.Where(be => be.GameID == id && be.IsHostTeam).OrderBy(be => be.Index).ToListAsync()
        };

        var visitorSide = new Scorecard()
        {
            Lineup = await dbContext.BattingLineupEntries.Where(ble => ble.GameID == id && !ble.IsHostTeam).OrderBy(ble => ble.Row).ToListAsync(),
            Events = await dbContext.BattingEvents.Where(be => be.GameID == id && !be.IsHostTeam).OrderBy(be => be.Index).ToListAsync()
        };

        return new JsonResult(new
        {
            hostSide,
            visitorSide
        });
    }

}