using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Executive")]
[Authorize(Policy = "Scope:Executive,Webmaster")]
public class APIExecutiveController(LeagueSitesContext dbContext) : ControllerBase
{
    [HttpGet("Dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var teams = await dbContext.Teams
            .AsNoTracking()
            .Where(t => !t.Hidden)
            .OrderByDescending(t => t.Active)
            .ThenBy(t => t.Name)
            .ToListAsync();

        var locations = await dbContext.Locations
            .AsNoTracking()
            .OrderByDescending(l => l.Active)
            .ThenBy(l => l.Name)
            .ToListAsync();

        var currentSeason = await dbContext.Seasons
            .AsNoTracking()
            .Where(s => s.Subseason == "Regular Season" && s.Year == DateTime.Now.Year)
            .Include(s => s.Games)
                .ThenInclude(g => g.Status)
            .Include(s => s.Tournaments)
                .ThenInclude(t => t.Brackets)
            .Include(s => s.Tournaments)
                .ThenInclude(t => t.RoundRobins)
            .FirstOrDefaultAsync();

        var currentPlayoffs = await dbContext.Seasons
            .AsNoTracking()
            .Where(s => s.Subseason == "Playoffs" && s.Year == DateTime.Now.Year)
            .Include(s => s.Tournaments)
                .ThenInclude(t => t.Brackets)
            .Include(s => s.Tournaments)
                .ThenInclude(t => t.RoundRobins)
            .FirstOrDefaultAsync();

        int gamesScheduled = 0;
        int gamesPlayed = 0;
        List<object>? seasonTournaments = null;

        if (currentSeason != null)
        {
            gamesScheduled = currentSeason.Games.Count(g =>
                g.Status!.Name == "Upcoming" || g.Status.Name == "Played" || g.Status.Name.StartsWith("Forfeit"));
            gamesPlayed = currentSeason.Games.Count(g =>
                g.Status!.Name == "Played" || g.Status.Name.StartsWith("Forfeit"));
            seasonTournaments = currentSeason.Tournaments.Select(t => new
            {
                id = t.ID,
                brackets = t.Brackets.Select(b => new { b.ID, b.Name }),
                roundRobins = t.RoundRobins.Select(r => new { r.ID, r.Name })
            }).Cast<object>().ToList();
        }

        List<object>? playoffTournaments = null;
        if (currentPlayoffs != null)
        {
            playoffTournaments = currentPlayoffs.Tournaments.Select(t => new
            {
                id = t.ID,
                brackets = t.Brackets.Select(b => new { b.ID, b.Name }),
                roundRobins = t.RoundRobins.Select(r => new { r.ID, r.Name })
            }).Cast<object>().ToList();
        }

        return Ok(new
        {
            teams = teams.Select(t => new TeamDetailDto(t)),
            locations = locations.Select(l => new LocationDetailDto(l)),
            currentSeason = currentSeason != null ? new
            {
                season = new SeasonSummaryDto(currentSeason),
                gamesScheduled,
                gamesPlayed,
                tournaments = seasonTournaments
            } : null,
            currentPlayoffs = currentPlayoffs != null ? new
            {
                season = new SeasonSummaryDto(currentPlayoffs),
                tournaments = playoffTournaments
            } : null
        });
    }

    [HttpPost("Season")]
    public async Task<IActionResult> CreateSeason()
    {
        var existing = await dbContext.Seasons
            .FirstOrDefaultAsync(s => s.Subseason == "Regular Season" && s.Year == DateTime.Now.Year);

        if (existing is not null)
            return Conflict("Season already exists for this year.");

        var season = new Season
        {
            Year = DateTime.Now.Year,
            Subseason = "Regular Season",
            StartDate = DateTime.Now.Date
        };

        dbContext.Seasons.Add(season);
        var result = await dbContext.SaveChangesAsync();

        if (result == 0)
            return StatusCode(500, "Error writing to database.");

        return CreatedAtAction(nameof(Dashboard), new { }, season);
    }

    [HttpPatch("Status/{entity}/{id:long}")]
    public async Task<IActionResult> ToggleStatus([FromRoute] string entity, [FromRoute] long id)
    {
        switch (entity.ToLower())
        {
            case "team":
                var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == id);
                if (team is null) return NotFound($"Team {id} not found.");
                team.Active = !team.Active;
                break;

            case "park":
                var park = await dbContext.Locations.FirstOrDefaultAsync(l => l.ID == id);
                if (park is null) return NotFound($"Park {id} not found.");
                park.Active = !park.Active;
                break;

            default:
                return BadRequest("Can only toggle status of 'team' or 'park' entities.");
        }

        if (await dbContext.SaveChangesAsync() == 0)
            return StatusCode(500, "Error updating in the database.");

        return NoContent();
    }

    /// <summary>
    /// Returns games with "Deleted" status (raccoon/recycle bin).
    /// </summary>
    [HttpGet("DeletedGames")]
    public async Task<IActionResult> DeletedGames()
    {
        var games = await dbContext.Games
            .AsNoTracking()
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Location)
            .Include(g => g.Status)
            .Include(g => g.SeriesGames)
            .Include(g => g.RoundRobinGames)
            .Where(g => g.Status!.Name == "Deleted")
            .OrderByDescending(g => g.Date)
            .ToListAsync();

        return Ok(games.Select(g => new GameSummaryDto(g)));
    }
}
