using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/TournamentBracket")]
[Authorize(Policy = "Scope:Executive,Webmaster")]
public class APITournamentBracketController(
    LeagueSitesContext dbContext,
    ITournamentService tournamentService) : ControllerBase
{
    /// <summary>
    /// Replaces a bracket's structure. Rounds and series that carry an ID are updated in
    /// place so their scheduled games survive the edit; anything dropped from the payload is
    /// removed, unless it still has a game attached.
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update([FromRoute] long id, [FromBody] BracketUpsertDto dto)
    {
        var bracket = await LoadAsync(id);
        if (bracket is null) return NotFound($"Bracket {id} not found.");

        try
        {
            TournamentStructureValidator.ValidateBracket(dto);
            await tournamentService.ValidateSeedingSourcesAsync(dto.Seeding);

            TournamentStructureMapper.Apply(bracket, dto);
            Reconcile(bracket, dto);

            await dbContext.SaveChangesAsync();

            await LogAsync($"/api/TournamentBracket/{id}", "Updated bracket", new
            {
                bracket.ID,
                bracket.Name,
                bracket.Format,
                bracket.SeedingConfiguration,
                Rounds = bracket.Rounds.Select(r => new { r.ID, r.Name, Series = r.Series.Count }),
            });

            return Ok(new { id = bracket.ID });
        }
        catch (TournamentFormatException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete([FromRoute] long id)
    {
        var bracket = await LoadAsync(id);
        if (bracket is null) return NotFound($"Bracket {id} not found.");

        var scheduled = bracket.Rounds
            .SelectMany(r => r.Series)
            .SelectMany(s => s.Games)
            .Count(g => g.GameID is not null);

        if (scheduled > 0)
            return BadRequest(
                $"\"{bracket.Name}\" has {scheduled} scheduled game(s). Remove them from the bracket before deleting it.");

        foreach (var round in bracket.Rounds.ToList())
        {
            foreach (var series in round.Series.ToList())
            {
                dbContext.SeriesGames.RemoveRange(series.Games);
                dbContext.RoundSeries.Remove(series);
            }
            dbContext.BracketRounds.Remove(round);
        }
        dbContext.TournamentBrackets.Remove(bracket);

        await dbContext.SaveChangesAsync();

        await LogAsync($"/api/TournamentBracket/{id}", "Deleted bracket",
            new { bracket.ID, bracket.Name, bracket.TournamentID });

        return NoContent();
    }

    // ------------------------------------------------------------- Reconciliation

    void Reconcile(TournamentBracket bracket, BracketUpsertDto dto)
    {
        var existingRounds = bracket.Rounds.ToDictionary(r => r.ID);
        var existingSeries = bracket.Rounds
            .SelectMany(r => r.Series)
            .ToDictionary(s => s.ID);

        var keptRounds = new List<BracketRound>();

        foreach (var roundDto in dto.Rounds)
        {
            BracketRound round;
            if (roundDto.ID is { } roundID)
            {
                if (!existingRounds.Remove(roundID, out var existing))
                    throw new TournamentFormatException(
                        $"Round {roundID} is not part of this bracket.");

                round = existing;
                TournamentStructureMapper.Apply(round, roundDto);
            }
            else
            {
                round = new BracketRound { Name = roundDto.Name.Trim(), BracketID = bracket.ID };
                bracket.Rounds.Add(round);
            }

            ReconcileSeries(round, roundDto, existingSeries);
            keptRounds.Add(round);
        }

        // Anything left unclaimed was dropped from the payload.
        foreach (var series in existingSeries.Values)
        {
            GuardAgainstDroppingSeries(series);
            dbContext.SeriesGames.RemoveRange(series.Games);
            dbContext.RoundSeries.Remove(series);
        }

        foreach (var round in existingRounds.Values)
        {
            dbContext.BracketRounds.Remove(round);
        }
    }

    void ReconcileSeries(
        BracketRound round, RoundUpsertDto roundDto, Dictionary<long, RoundSeries> existingSeries)
    {
        foreach (var seriesDto in roundDto.Series)
        {
            if (seriesDto.ID is { } seriesID)
            {
                if (!existingSeries.Remove(seriesID, out var series))
                    throw new TournamentFormatException(
                        $"Series {seriesID} is not part of this bracket.");

                GuardAgainstShorteningSeries(series, seriesDto);
                TournamentStructureMapper.Apply(series, seriesDto);

                // A series can be moved to a different round by sending it under that round.
                if (series.RoundID != round.ID)
                {
                    series.Round?.Series.Remove(series);
                    series.Round = round;
                    round.Series.Add(series);
                }
            }
            else
            {
                round.Series.Add(TournamentStructureMapper.BuildSeries(seriesDto));
            }
        }
    }

    static void GuardAgainstDroppingSeries(RoundSeries series)
    {
        if (series.Games.Any(g => g.GameID is not null))
            throw new TournamentFormatException(
                $"Series {series.Number} still has a scheduled game, so it cannot be removed from the bracket. " +
                "Remove its games first.");
    }

    static void GuardAgainstShorteningSeries(RoundSeries series, SeriesUpsertDto dto)
    {
        var newLength = dto.HostOrder.Count;
        var orphaned = series.Games
            .Where(g => g.GameID is not null && g.GameNumber > newLength)
            .Select(g => g.GameNumber)
            .OrderBy(n => n)
            .ToList();

        if (orphaned.Count > 0)
            throw new TournamentFormatException(
                $"Series {series.Number} would be shortened to {newLength} game(s), but game " +
                $"{string.Join(" and ", orphaned)} is already scheduled. Remove those games first.");
    }

    // -------------------------------------------------------------------- Helpers

    async Task<TournamentBracket?> LoadAsync(long id) =>
        await dbContext.TournamentBrackets
            .AsSplitQuery()
            .Include(b => b.Rounds)
                .ThenInclude(r => r.Series)
                    .ThenInclude(s => s.Games)
            .FirstOrDefaultAsync(b => b.ID == id);

    async Task LogAsync(string resource, string summary, object payload)
    {
        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(EventType.Update, uid, resource, summary, payload));
        await dbContext.SaveChangesAsync();
    }
}
