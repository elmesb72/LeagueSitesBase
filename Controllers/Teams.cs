using Facet.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Teams")]
public class APITeamsController(LeagueSitesContext context) : ControllerBase
{
    readonly LeagueSitesContext dbContext = context;

    [ResponseCache(Duration = 30)]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var teams = await dbContext.Teams
            .Where(t => t.Active)
            .OrderBy(t => t.Name)
            .SelectFacet<TeamDetailDto>()
            .ToListAsync();

        return Ok(teams);
    }

    [ResponseCache(Duration = 30)]
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get([FromRoute] long id)
    {
        var team = await dbContext.Teams
            .Where(t => t.ID == id)
            .SelectFacet<TeamDetailDto>()
            .FirstOrDefaultAsync();

        if (team is null)
            return NotFound();

        return Ok(team);
    }

    /// Returns a dictionary of names and jersey numbers for active players on the given team.
    /// Optional query parameter: exclude (string) removes a player matching the provided number.
    [ResponseCache(Duration = 30)]
    [HttpGet("{id:long}/Players")]
    public async Task<IActionResult> GetPlayers([FromRoute] long id, [FromQuery] string? exclude)
    {
        var players = await dbContext.Invitations
            .AsNoTracking()
            .Include(i => i.Status)
            .Where(i => i.Status!.Name == "Active")
            .Where(i => i.TeamID == id)
            .Where(i => i.PlayerID != null)
            .Select(i => i.Player!)
            .Where(p => !string.IsNullOrEmpty(p.Number))
            .ToListAsync();

        if (!string.IsNullOrEmpty(exclude))
            players = players.Where(p => p.Number != exclude).ToList();

        return Ok(players.ToDictionary(p => p.Number!, p => p.Name));
    }
}
