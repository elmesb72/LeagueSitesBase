using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Game")]
public class APIGameController(
    LeagueSitesContext dbContext,
    IAuthorizationService authorizationService) : ControllerBase
{
    public record GameUpsertDto(
        long SeasonID,
        DateTime Date,
        long HostTeamID,
        long VisitingTeamID,
        long LocationID,
        long StatusID,
        long? ScoreHost,
        long? ScoreVisitor);

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get([FromRoute] long id)
    {
        var game = await dbContext.Games
            .AsNoTracking()
            .Include(g => g.Season)
            .Include(g => g.Location)
            .Include(g => g.Status)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .FirstOrDefaultAsync(g => g.ID == id);

        if (game is null)
            return NotFound();

        // Resource-based auth: checks site roles + team roles against this game's teams
        var canEdit = (await authorizationService.AuthorizeAsync(
            User, game,
            new TeamScopedRequirement(
                PermissionsScope.Manager, PermissionsScope.Scorer,
                PermissionsScope.Executive, PermissionsScope.Webmaster))).Succeeded;

        return Ok(new { game, canEdit });
    }

    // Site-level scope check via the policy provider convention
    [Authorize(Policy = "Scope:Manager,Scorer,Executive,Webmaster")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] GameUpsertDto dto)
    {
        var game = new Game
        {
            SeasonID = dto.SeasonID,
            Date = dto.Date,
            HostTeamID = dto.HostTeamID,
            VisitingTeamID = dto.VisitingTeamID,
            LocationID = dto.LocationID,
            StatusID = dto.StatusID,
            ScoreHost = dto.ScoreHost,
            ScoreVisitor = dto.ScoreVisitor
        };

        var status = await dbContext.GameStatuses.FirstAsync(gs => gs.ID == game.StatusID);
        if (status.Name != "Played")
        {
            game.ScoreHost = null;
            game.ScoreVisitor = null;
        }

        await dbContext.Games.AddAsync(game);
        await dbContext.SaveChangesAsync();

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Game", "Added new game",
            JsonConvert.SerializeObject(game)));
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = game.ID }, game);
    }

    [Authorize]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update([FromRoute] long id, [FromBody] GameUpsertDto dto)
    {
        var game = await dbContext.Games
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .FirstOrDefaultAsync(g => g.ID == id);

        if (game is null)
            return NotFound();

        // Resource-based auth: checks against this game's specific teams
        var auth = await authorizationService.AuthorizeAsync(
            User, game,
            new TeamScopedRequirement(
                PermissionsScope.Manager, PermissionsScope.Scorer,
                PermissionsScope.Executive, PermissionsScope.Webmaster));

        if (!auth.Succeeded)
            return Forbid();

        game.SeasonID = dto.SeasonID;
        game.Date = dto.Date;
        game.HostTeamID = dto.HostTeamID;
        game.VisitingTeamID = dto.VisitingTeamID;
        game.LocationID = dto.LocationID;
        game.StatusID = dto.StatusID;
        game.ScoreHost = dto.ScoreHost;
        game.ScoreVisitor = dto.ScoreVisitor;

        var status = await dbContext.GameStatuses.FirstAsync(gs => gs.ID == game.StatusID);
        if (status.Name != "Played")
        {
            game.ScoreHost = null;
            game.ScoreVisitor = null;
        }

        dbContext.Games.Update(game);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Game/" + id, "Updated game",
            JsonConvert.SerializeObject(game)));

        await dbContext.SaveChangesAsync();

        return Ok(game);
    }
}
