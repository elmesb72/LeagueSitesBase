using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Playoffs")]
public class APIPlayoffsController(LeagueSitesContext dbContext, ISeasonService seasonService) : ControllerBase
{
    [ResponseCache(Duration = 30)]
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? year)
    {
        Season? currentSeason;
        if (year == null)
        {
            currentSeason = await seasonService.GetClosestSeasonAsync();
        }
        else
        {
            currentSeason = await dbContext.Seasons
                .FirstOrDefaultAsync(s => s.Year == year && s.Subseason == "Playoffs");
        }

        if (currentSeason is null)
            return NotFound();

        var playoffs = await dbContext.Seasons
            .Include(s => s.Tournaments)
                .ThenInclude(t => t.Brackets)
                    .ThenInclude(b => b.Rounds)
                        .ThenInclude(r => r.Series)
                            .ThenInclude(s => s.Games)
                                .ThenInclude(g => g.Game)
            .Include(s => s.Tournaments)
                .ThenInclude(t => t.RoundRobins)
                    .ThenInclude(r => r.Games)
                        .ThenInclude(g => g.Game)
            .Where(s => s.Year == currentSeason.Year && s.Subseason == "Playoffs")
            .FirstOrDefaultAsync();

        if (playoffs is null)
            return NotFound();

        var playoffGames = await dbContext.Games
            .AsNoTracking()
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Status)
            .Include(g => g.Location)
            .Where(g => g.SeasonID == playoffs.ID)
            .ToListAsync();

        var tournament = playoffs.Tournaments.FirstOrDefault();
        tournament?.Populate(playoffGames, dbContext);

        if (tournament is null)
            return Ok(new PlayoffsDto(new SeasonSummaryDto(playoffs), [], []));

        return Ok(new PlayoffsDto(
            new SeasonSummaryDto(playoffs),
            tournament.Brackets.Select(BracketDto.From).ToList(),
            tournament.RoundRobins.Select(RoundRobinDto.From).ToList()));
    }
}
