using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/TournamentRoundRobin")]
[Authorize(Policy = "Scope:Executive,Webmaster")]
public class APITournamentRoundRobinController(
    LeagueSitesContext dbContext,
    ITournamentService tournamentService) : ControllerBase
{
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update([FromRoute] long id, [FromBody] RoundRobinUpsertDto dto)
    {
        var roundRobin = await dbContext.TournamentRoundRobins.FirstOrDefaultAsync(r => r.ID == id);
        if (roundRobin is null) return NotFound($"Pool {id} not found.");

        try
        {
            TournamentStructureValidator.ValidateRoundRobin(dto);
            await tournamentService.ValidateSeedingSourcesAsync(dto.Seeding);

            TournamentStructureMapper.Apply(roundRobin, dto);
            await dbContext.SaveChangesAsync();

            await LogAsync($"/api/TournamentRoundRobin/{id}", "Updated round robin pool", new
            {
                roundRobin.ID,
                roundRobin.Name,
                roundRobin.Historical,
                roundRobin.SeedingConfiguration,
            });

            return Ok(new { id = roundRobin.ID });
        }
        catch (TournamentFormatException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete([FromRoute] long id)
    {
        var roundRobin = await dbContext.TournamentRoundRobins
            .Include(r => r.Games)
            .FirstOrDefaultAsync(r => r.ID == id);

        if (roundRobin is null) return NotFound($"Pool {id} not found.");

        var scheduled = roundRobin.Games.Count(g => g.GameID is not null);
        if (scheduled > 0)
            return BadRequest(
                $"\"{roundRobin.Name}\" has {scheduled} scheduled game(s). Remove them before deleting the pool.");

        dbContext.RoundRobinGames.RemoveRange(roundRobin.Games);
        dbContext.TournamentRoundRobins.Remove(roundRobin);
        await dbContext.SaveChangesAsync();

        await LogAsync($"/api/TournamentRoundRobin/{id}", "Deleted round robin pool",
            new { roundRobin.ID, roundRobin.Name, roundRobin.TournamentID });

        return NoContent();
    }

    /// <summary>
    /// Adds a game to a pool. Pools have no host order, so both teams are chosen explicitly.
    /// </summary>
    [HttpPost("{id:long}/Game")]
    public async Task<IActionResult> ScheduleGame([FromRoute] long id, [FromBody] RoundRobinGameScheduleDto dto)
    {
        var roundRobin = await dbContext.TournamentRoundRobins
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r => r.ID == id);

        if (roundRobin is null) return NotFound($"Pool {id} not found.");
        if (roundRobin.Tournament is null) return BadRequest($"Pool {id} is not attached to a tournament.");

        if (dto.HostTeamID == dto.VisitingTeamID)
            return BadRequest("A game needs two different teams.");

        var host = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == dto.HostTeamID);
        if (host is null) return BadRequest($"Team {dto.HostTeamID} not found.");

        var visitor = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == dto.VisitingTeamID);
        if (visitor is null) return BadRequest($"Team {dto.VisitingTeamID} not found.");

        var location = await dbContext.Locations.FirstOrDefaultAsync(l => l.ID == dto.LocationID);
        if (location is null) return BadRequest($"Park {dto.LocationID} not found.");

        var upcoming = await dbContext.GameStatuses.FirstAsync(s => s.Name == "Upcoming");

        var game = new Game
        {
            SeasonID = roundRobin.Tournament.SeasonID,
            Date = dto.Date,
            HostTeamID = host.ID,
            VisitingTeamID = visitor.ID,
            LocationID = location.ID,
            StatusID = upcoming.ID,
        };
        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();

        dbContext.RoundRobinGames.Add(new RoundRobinGame
        {
            TournamentRoundRobinID = roundRobin.ID,
            GameID = game.ID,
        });
        await dbContext.SaveChangesAsync();

        await LogAsync($"/api/TournamentRoundRobin/{id}/Game", $"Scheduled game in pool {roundRobin.Name}", new
        {
            RoundRobinID = roundRobin.ID,
            roundRobin.Name,
            GameID = game.ID,
            game.Date,
            Host = host.FullName,
            Visitor = visitor.FullName,
            Park = location.Name,
        });

        var created = await dbContext.Games
            .AsNoTracking()
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Location)
            .Include(g => g.Status)
            .Include(g => g.Season)
            .FirstAsync(g => g.ID == game.ID);

        return Ok(new GameSummaryDto(created));
    }

    /// <summary>
    /// Takes a game out of a pool and moves it to the deleted bin, where it can be recovered.
    /// </summary>
    [HttpDelete("Game/{roundRobinGameID:long}")]
    public async Task<IActionResult> RemoveGame([FromRoute] long roundRobinGameID)
    {
        var link = await dbContext.RoundRobinGames
            .FirstOrDefaultAsync(g => g.ID == roundRobinGameID);

        if (link is null) return NotFound($"Pool game {roundRobinGameID} not found.");

        var game = link.GameID is null
            ? null
            : await dbContext.Games.FirstOrDefaultAsync(g => g.ID == link.GameID);

        if (game is not null)
        {
            var deleted = await dbContext.GameStatuses.FirstAsync(s => s.Name == "Deleted");
            game.StatusID = deleted.ID;
            game.ScoreHost = null;
            game.ScoreVisitor = null;
            dbContext.Games.Update(game);
        }

        dbContext.RoundRobinGames.Remove(link);
        await dbContext.SaveChangesAsync();

        await LogAsync($"/api/TournamentRoundRobin/Game/{roundRobinGameID}", "Removed game from pool", new
        {
            RoundRobinGameID = roundRobinGameID,
            link.TournamentRoundRobinID,
            link.GameID,
        });

        return NoContent();
    }

    async Task LogAsync(string resource, string summary, object payload)
    {
        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(EventType.Update, uid, resource, summary, payload));
        await dbContext.SaveChangesAsync();
    }
}
