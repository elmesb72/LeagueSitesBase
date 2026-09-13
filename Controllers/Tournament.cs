using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Tournament")]
[Authorize(Policy = "Scope:Executive,Webmaster")]
public class APITournamentController(
    LeagueSitesContext dbContext,
    ITournamentService tournamentService) : ControllerBase
{
    /// <summary>
    /// Full structured view of a tournament: brackets, rounds, series, pools, every resolved
    /// team, plus the reference data the management UI needs to offer choices.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get([FromRoute] long id)
    {
        var tournament = await tournamentService.GetPopulatedAsync(id);
        if (tournament is null) return NotFound($"Tournament {id} not found.");

        return Ok(await tournamentService.BuildDetailAsync(tournament));
    }

    /// <summary>
    /// The tournament shown for a season, by season id — the same choice the public
    /// Playoffs page makes (the season's first tournament). Lets an executive jump from
    /// the public page for any year straight to its editor; the class-level policy
    /// doubles as the "may edit" check, so callers treat 403 as "no button".
    /// </summary>
    [HttpGet("ForSeason/{seasonId:long}")]
    public async Task<IActionResult> ForSeason([FromRoute] long seasonId)
    {
        var tournamentId = await dbContext.Tournaments
            .Where(t => t.SeasonID == seasonId)
            .OrderBy(t => t.ID)
            .Select(t => (long?)t.ID)
            .FirstOrDefaultAsync();
        if (tournamentId is null) return NotFound($"Season {seasonId} has no tournament.");

        return Ok(new { id = tournamentId.Value });
    }

    /// <summary>Default seeding rule to offer when adding a bracket or pool.</summary>
    [HttpGet("{id:long}/DefaultSeeding")]
    public async Task<IActionResult> DefaultSeeding([FromRoute] long id)
    {
        var tournament = await tournamentService.GetPopulatedAsync(id);
        if (tournament is null) return NotFound($"Tournament {id} not found.");

        return Ok(await tournamentService.GetDefaultSeedingAsync(tournament));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TournamentCreateDto dto)
    {
        var season = await dbContext.Seasons.FirstOrDefaultAsync(s => s.ID == dto.SeasonID);
        if (season is null) return NotFound($"Season {dto.SeasonID} not found.");

        var tournament = new Tournament { SeasonID = season.ID };
        dbContext.Tournaments.Add(tournament);
        await dbContext.SaveChangesAsync();

        await LogAsync("/api/Tournament", "Created tournament",
            new { tournament.ID, Season = new SeasonSummaryDto(season) });

        return CreatedAtAction(nameof(Get), new { id = tournament.ID }, new { id = tournament.ID });
    }

    [HttpPost("{id:long}/Bracket")]
    public async Task<IActionResult> CreateBracket([FromRoute] long id, [FromBody] BracketUpsertDto dto)
    {
        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(t => t.ID == id);
        if (tournament is null) return NotFound($"Tournament {id} not found.");

        try
        {
            TournamentStructureValidator.ValidateBracket(dto);
            await tournamentService.ValidateSeedingSourcesAsync(dto.Seeding);

            var bracket = new TournamentBracket
            {
                TournamentID = tournament.ID,
                Name = dto.Name.Trim(),
                Format = dto.Format,
                Historical = dto.Historical,
            };
            TournamentStructureMapper.Apply(bracket, dto);

            foreach (var round in dto.Rounds)
                bracket.Rounds.Add(TournamentStructureMapper.BuildRound(round));

            dbContext.TournamentBrackets.Add(bracket);
            await dbContext.SaveChangesAsync();

            await LogAsync($"/api/Tournament/{id}/Bracket", "Created bracket", new
            {
                bracket.ID,
                bracket.Name,
                bracket.Format,
                bracket.SeedingConfiguration,
                Rounds = bracket.Rounds.Select(r => new { r.ID, r.Name, Series = r.Series.Count }),
            });

            return CreatedAtAction(nameof(Get), new { id = tournament.ID }, new { id = bracket.ID });
        }
        catch (TournamentFormatException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id:long}/RoundRobin")]
    public async Task<IActionResult> CreateRoundRobin([FromRoute] long id, [FromBody] RoundRobinUpsertDto dto)
    {
        var tournament = await dbContext.Tournaments.FirstOrDefaultAsync(t => t.ID == id);
        if (tournament is null) return NotFound($"Tournament {id} not found.");

        try
        {
            TournamentStructureValidator.ValidateRoundRobin(dto);
            await tournamentService.ValidateSeedingSourcesAsync(dto.Seeding);

            var roundRobin = new TournamentRoundRobin
            {
                TournamentID = tournament.ID,
                Name = dto.Name.Trim(),
                Historical = dto.Historical,
            };
            TournamentStructureMapper.Apply(roundRobin, dto);

            dbContext.TournamentRoundRobins.Add(roundRobin);
            await dbContext.SaveChangesAsync();

            await LogAsync($"/api/Tournament/{id}/RoundRobin", "Created round robin pool", new
            {
                roundRobin.ID,
                roundRobin.Name,
                roundRobin.SeedingConfiguration,
            });

            return CreatedAtAction(nameof(Get), new { id = tournament.ID }, new { id = roundRobin.ID });
        }
        catch (TournamentFormatException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    async Task LogAsync(string resource, string summary, object payload)
    {
        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(EventType.Update, uid, resource, summary, payload));
        await dbContext.SaveChangesAsync();
    }
}
