using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Series")]
[Authorize(Policy = "Scope:Executive,Webmaster")]
public class APITournamentSeriesController(
    LeagueSitesContext dbContext,
    ITournamentService tournamentService) : ControllerBase
{
    /// <summary>
    /// Schedules one game of a series. The executive supplies only a date and a park: who
    /// hosts is determined by the series' host order and the teams currently filling its
    /// spots, so a playoff game can never be created with the wrong home team.
    /// </summary>
    [HttpPost("{id:long}/Game")]
    public async Task<IActionResult> ScheduleGame([FromRoute] long id, [FromBody] SeriesGameScheduleDto dto)
    {
        var series = await dbContext.RoundSeries
            .AsNoTracking()
            .Include(s => s.Round)
                .ThenInclude(r => r!.Bracket)
            .FirstOrDefaultAsync(s => s.ID == id);

        if (series is null) return NotFound($"Series {id} not found.");

        var tournamentID = series.Round?.Bracket?.TournamentID;
        if (tournamentID is null)
            return BadRequest($"Series {id} is not attached to a bracket.");

        var tournament = await tournamentService.GetPopulatedAsync(tournamentID.Value);
        if (tournament is null) return NotFound($"Tournament {tournamentID} not found.");

        var resolved = tournament.Brackets
            .SelectMany(b => b.Rounds)
            .SelectMany(r => r.Series)
            .FirstOrDefault(s => s.ID == id);

        if (resolved is null) return NotFound($"Series {id} not found in tournament {tournamentID}.");

        if (resolved.Games.Any(g => g.GameNumber == dto.GameNumber && g.GameID is not null))
            return BadRequest($"Game {dto.GameNumber} of this series is already scheduled.");

        var location = await dbContext.Locations.FirstOrDefaultAsync(l => l.ID == dto.LocationID);
        if (location is null) return BadRequest($"Park {dto.LocationID} not found.");

        Team host, visitor;
        try
        {
            (host, visitor) = tournamentService.ResolveGameTeams(resolved, dto.GameNumber);
        }
        catch (TournamentFormatException ex)
        {
            return BadRequest(ex.Message);
        }

        var upcoming = await dbContext.GameStatuses.FirstAsync(s => s.Name == "Upcoming");

        var game = new Game
        {
            SeasonID = tournament.SeasonID,
            Date = dto.Date,
            HostTeamID = host.ID,
            VisitingTeamID = visitor.ID,
            LocationID = location.ID,
            StatusID = upcoming.ID,
        };
        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();

        dbContext.SeriesGames.Add(new SeriesGame
        {
            SeriesID = resolved.ID,
            GameNumber = dto.GameNumber,
            GameID = game.ID,
        });
        await dbContext.SaveChangesAsync();

        await LogAsync($"/api/Series/{id}/Game", $"Scheduled game {dto.GameNumber} of series {resolved.Number}", new
        {
            SeriesID = resolved.ID,
            resolved.Number,
            dto.GameNumber,
            GameID = game.ID,
            game.Date,
            Host = host.FullName,
            Visitor = visitor.FullName,
            Park = location.Name,
        });

        var created = await LoadGameAsync(game.ID);
        return Ok(new GameSummaryDto(created!));
    }

    /// <summary>
    /// Takes a game out of a series and moves it to the deleted bin, where it can be
    /// recovered if this was a mistake.
    /// </summary>
    [HttpDelete("Game/{seriesGameID:long}")]
    public async Task<IActionResult> RemoveGame([FromRoute] long seriesGameID)
    {
        var link = await dbContext.SeriesGames
            .Include(sg => sg.Series)
            .FirstOrDefaultAsync(sg => sg.ID == seriesGameID);

        if (link is null) return NotFound($"Series game {seriesGameID} not found.");

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

        dbContext.SeriesGames.Remove(link);
        await dbContext.SaveChangesAsync();

        await LogAsync($"/api/Series/Game/{seriesGameID}",
            $"Removed game {link.GameNumber} from series {link.Series?.Number}", new
            {
                SeriesGameID = seriesGameID,
                link.SeriesID,
                link.GameNumber,
                link.GameID,
            });

        return NoContent();
    }

    async Task<Game?> LoadGameAsync(long id) =>
        await dbContext.Games
            .AsNoTracking()
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Location)
            .Include(g => g.Status)
            .Include(g => g.Season)
            .FirstOrDefaultAsync(g => g.ID == id);

    async Task LogAsync(string resource, string summary, object payload)
    {
        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(EventType.Update, uid, resource, summary, payload));
        await dbContext.SaveChangesAsync();
    }
}
