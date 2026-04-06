using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api")]
public class APITeamsController(LeagueSitesContext context) : ControllerBase
{
    readonly LeagueSitesContext dbContext = context;

    /// Returns a dictionary of names and jersey numbers for active players on the given team
    /// Optional parameters via Querystring: 
    /// exclude (string): removes a player from the results if matches the provided number
    [HttpGet("Teams")]
    public async Task<IActionResult> OnGetAsync()
    {
        var teams = await dbContext.Teams
            .Where(t => t.Active)
            .ToListAsync();

        return new JsonResult(teams);
    }
}