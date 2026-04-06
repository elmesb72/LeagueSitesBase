using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Game")]
public class APIGameController(
    LeagueSitesContext dbContext,
    IAuthorizationService authorizationService) : ControllerBase
{
    readonly LeagueSitesContext dbContext = dbContext;
    readonly IAuthorizationService authorizationService = authorizationService;

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
        {
            return NotFound();
        }

        var canEditResult = await authorizationService.AuthorizeAsync(User, game, "CanEditGame");
        var canEdit = canEditResult.Succeeded;
        return Ok(new { game, canEdit });
    }

    /*// Mirrors the "id == null && user can create" branch in Pages/Game.cshtml.cs
    [Authorize]
    [HttpGet("New")]
    public async Task<IActionResult> New([FromQuery] string? date, [FromQuery] int? location)
    {
        var permissions = await permissionsService.GetAsync(User);
        if (!permissions.Allow("CreateGame"))
        {
            return Forbid();
        }

        var dateIncluded = DateTime.TryParse(date, out var parsedDate);
        var game = new Game
        {
            ID = -1,
            Date = dateIncluded ? parsedDate : DateTime.Today,
            LocationID = location ?? 0,
            StatusID = 1
        };

        return Ok(game);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] GameUpsertDto dto)
    {
        var permissions = await permissionsService.GetAsync(User);
        if (!permissions.Allow("CreateGame"))
        {
            return Forbid();
        }

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

        await dbContext.Games.AddAsync(game);
        await dbContext.SaveChangesAsync();

        var uid = User.Identity?.IsAuthenticated == true
            ? Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value)
            : -1;

        await dbContext.Events.AddAsync(Event.Log(
            EventType.Update,
            uid,
            "/api/Game",
            "Added new game",
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
        {
            return NotFound();
        }

        var teams = new List<Team>();
        if (game.HostTeam != null) teams.Add(game.HostTeam);
        if (game.VisitingTeam != null) teams.Add(game.VisitingTeam);

        var permissions = await permissionsService.GetAsync(User, teams);
        var canEdit =
            permissions.Include([PermissionsScope.Webmaster, PermissionsScope.Executive]) ||
            permissions.Include([PermissionsScope.Manager, PermissionsScope.Scorer], game.HostTeam) ||
            permissions.Include([PermissionsScope.Manager, PermissionsScope.Scorer], game.VisitingTeam);

        if (!canEdit)
        {
            return Forbid();
        }

        game.SeasonID = dto.SeasonID;
        game.Date = dto.Date;
        game.HostTeamID = dto.HostTeamID;
        game.VisitingTeamID = dto.VisitingTeamID;
        game.LocationID = dto.LocationID;
        game.StatusID = dto.StatusID;
        game.ScoreHost = dto.ScoreHost;
        game.ScoreVisitor = dto.ScoreVisitor;

        var statusName = (await dbContext.GameStatuses.FirstAsync(gs => gs.ID == game.StatusID)).Name;
        if (statusName != "Played")
        {
            game.ScoreHost = null;
            game.ScoreVisitor = null;
        }

        dbContext.Games.Update(game);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update,
            uid,
            "/api/Game/" + id,
            "Updated game",
            JsonConvert.SerializeObject(game)));

        await dbContext.SaveChangesAsync();

        return Ok(game);
    }*/
}

