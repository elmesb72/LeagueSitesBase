using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Game")]
public class APIGameController(
    LeagueSitesContext dbContext,
    IAuthorizationService authorizationService,
    IPermissionsService permissionsService,
    IStandingsConfigService standingsConfigService) : ControllerBase
{
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

        var canDelete = false;
        object? editData = null;
        if (canEdit)
        {
            var permissions = await permissionsService.GetAsync(User);
            canDelete = permissions.Include([PermissionsScope.Executive, PermissionsScope.Webmaster]);

            editData = new
            {
                seasons = await dbContext.Seasons.AsNoTracking()
                    .OrderByDescending(s => s.StartDate)
                    .Select(s => new { s.ID, s.Name })
                    .ToListAsync(),
                teams = await dbContext.Teams.AsNoTracking()
                    .Where(t => t.Active)
                    .OrderBy(t => t.Abbreviation)
                    .Select(t => new { t.ID, t.FullName, t.Abbreviation, t.BackgroundColor, t.Color })
                    .ToListAsync(),
                locations = await dbContext.Locations.AsNoTracking()
                    .OrderByDescending(l => l.Active)
                    .ThenBy(l => l.Name)
                    .Select(l => new { l.ID, l.Name })
                    .ToListAsync(),
                statuses = await dbContext.GameStatuses.AsNoTracking()
                    .Select(s => new { s.ID, s.Name })
                    .ToListAsync()
            };
        }

        // Records to date
        var seasonGames = await dbContext.Games
            .AsNoTracking()
            .Include(g => g.Status)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Where(g => g.Status!.Name != "Deleted"
                && g.SeasonID == game.SeasonID
                && g.Date <= game.Date)
            .ToListAsync();
        // Config matters here for the forfeit score baked into W-L-T records.
        var standings = new Standings(seasonGames, await standingsConfigService.GetAsync());
        string? hostRecord = standings.ContainsKey(game.HostTeam!) ? standings[game.HostTeam!].ToString() : null;
        string? visitorRecord = standings.ContainsKey(game.VisitingTeam!) ? standings[game.VisitingTeam!].ToString() : null;

        return Ok(new { game = new GameDetailDto(game), canEdit, canDelete, editData, hostRecord, visitorRecord });
    }

    [Authorize(Policy = "Scope:Manager,Scorer,Executive,Webmaster")]
    [HttpGet("Create")]
    public async Task<IActionResult> GetCreateData()
    {
        return Ok(new
        {
            seasons = await dbContext.Seasons.AsNoTracking()
                .OrderByDescending(s => s.StartDate)
                .Select(s => new { s.ID, s.Name })
                .ToListAsync(),
            teams = await dbContext.Teams.AsNoTracking()
                .Where(t => t.Active)
                .OrderBy(t => t.Abbreviation)
                .Select(t => new { t.ID, t.FullName, t.Abbreviation, t.BackgroundColor, t.Color })
                .ToListAsync(),
            locations = await dbContext.Locations.AsNoTracking()
                .Where(l => l.Active)
                .OrderBy(l => l.Name)
                .Select(l => new { l.ID, l.Name })
                .ToListAsync(),
            statuses = await dbContext.GameStatuses.AsNoTracking()
                .Where(s => s.Name != "Deleted")
                .Select(s => new { s.ID, s.Name })
                .ToListAsync()
        });
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
        if (status.Name == "Played")
        {
            if (dto.ScoreHost is null || dto.ScoreVisitor is null)
                return BadRequest("Both scores must be provided when the game status is Played.");
        }
        else
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
            new GameDetailDto(game)));
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = game.ID }, new GameDetailDto(game));
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
        if (status.Name == "Played")
        {
            if (dto.ScoreHost is null || dto.ScoreVisitor is null)
                return BadRequest("Both scores must be provided when the game status is Played.");
        }
        else
        {
            game.ScoreHost = null;
            game.ScoreVisitor = null;
        }

        dbContext.Games.Update(game);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Game/" + id, "Updated game",
            new GameDetailDto(game)));

        await dbContext.SaveChangesAsync();

        return Ok(new GameDetailDto(game));
    }

    [Authorize(Policy = "Scope:Executive,Webmaster")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete([FromRoute] long id)
    {
        var game = await dbContext.Games
            .Include(g => g.Status)
            .FirstOrDefaultAsync(g => g.ID == id);

        if (game is null)
            return NotFound();

        var deletedStatus = await dbContext.GameStatuses.FirstAsync(gs => gs.Name == "Deleted");
        game.StatusID = deletedStatus.ID;
        game.ScoreHost = null;
        game.ScoreVisitor = null;

        dbContext.Games.Update(game);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Game/" + id, "Deleted game",
            new GameDetailDto(game)));

        await dbContext.SaveChangesAsync();

        return NoContent();
    }
}
