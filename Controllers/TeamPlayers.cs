using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api")]
public class APITeamPlayersController(LeagueSitesContext context) : ControllerBase
{
    readonly LeagueSitesContext dbContext = context;

    /// Returns a dictionary of names and jersey numbers for active players on the given team
    /// Optional parameters via Querystring: 
    /// exclude (string): removes a player from the results if matches the provided number
    [HttpGet("TeamPlayers/{id}")]
    public async Task<IActionResult> OnGetAsync([FromRoute] int id)
    {
        var players = await dbContext.Invitations
            .Include(i => i.Status)
            .Where(i => i.Status!.Name == "Active")
            .Where(i => i.TeamID == (long)id)
            .Where(i => i.PlayerID != null)
            .Select(i => i.Player!)
            .Where(p => !string.IsNullOrEmpty(p.Number))
            .ToListAsync();

        if (Request.Query.ContainsKey("exclude"))
        {
            players = players.Where(p => p.Number != Request.Query["exclude"]).ToList();
        }

        return new JsonResult(players.ToDictionary(p => p.Number!, p => p.Name));
    }
}