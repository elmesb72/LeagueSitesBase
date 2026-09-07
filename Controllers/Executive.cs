using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Executive")]
[Authorize(Policy = "Scope:Executive,Webmaster")]
public class APIExecutiveController(
    LeagueSitesContext dbContext,
    IScheduleImportService scheduleImportService) : ControllerBase
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

        // Efficient bulk check: collect IDs of teams/locations that have any associated record
        var referencedTeamIds = new HashSet<long>(
            await dbContext.Games.AsNoTracking()
                .Select(g => g.HostTeamID).Distinct().ToListAsync());
        referencedTeamIds.UnionWith(
            await dbContext.Games.AsNoTracking()
                .Select(g => g.VisitingTeamID).Distinct().ToListAsync());
        referencedTeamIds.UnionWith(
            await dbContext.Invitations.AsNoTracking()
                .Select(i => i.TeamID).Distinct().ToListAsync());
        referencedTeamIds.UnionWith(
            await dbContext.Set<TeamSocial>().AsNoTracking()
                .Select(s => s.TeamID).Distinct().ToListAsync());

        var referencedLocationIds = new HashSet<long>(
            await dbContext.Games.AsNoTracking()
                .Select(g => g.LocationID).Distinct().ToListAsync());

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
            teams = teams.Select(t =>
            {
                var dto = new TeamDetailDto(t);
                return new
                {
                    dto.ID,
                    dto.Location,
                    dto.Name,
                    dto.FullName,
                    dto.Abbreviation,
                    dto.Active,
                    dto.Hidden,
                    dto.BackgroundColor,
                    dto.Color,
                    CanDelete = !referencedTeamIds.Contains(t.ID)
                };
            }),
            locations = locations.Select(l =>
            {
                var dto = new LocationDetailDto(l);
                return new
                {
                    dto.ID,
                    dto.Active,
                    dto.Name,
                    dto.FormalName,
                    dto.City,
                    dto.Address,
                    dto.MapsPlaceID,
                    CanDelete = !referencedLocationIds.Contains(l.ID)
                };
            }),
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

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);

        if (result == 0)
        {
            dbContext.Events.Add(Event.Log(
                EventType.Error, uid,
                "/api/Executive/Season", "Season creation failed — no rows written",
                new { season.Year, season.Subseason }));
            await dbContext.SaveChangesAsync();
            return StatusCode(500, "Error writing to database.");
        }

        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Executive/Season", "Created season",
            new SeasonSummaryDto(season)));
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(Dashboard), new { }, season);
    }

    /// <summary>
    /// Creates this year's playoffs season together with an empty tournament to hold its
    /// brackets and pools, so an executive gets a single "start the playoffs" action.
    /// </summary>
    [HttpPost("Season/Playoffs")]
    public async Task<IActionResult> CreatePlayoffs()
    {
        var regularSeason = await dbContext.Seasons
            .FirstOrDefaultAsync(s => s.Subseason == "Regular Season" && s.Year == DateTime.Now.Year);

        if (regularSeason is null)
            return BadRequest($"Create the {DateTime.Now.Year} regular season before setting up playoffs.");

        var existing = await dbContext.Seasons
            .Include(s => s.Tournaments)
            .FirstOrDefaultAsync(s => s.Subseason == "Playoffs" && s.Year == DateTime.Now.Year);

        if (existing is not null)
        {
            // Idempotent: an existing playoffs season without a tournament still needs one.
            var existingTournament = existing.Tournaments.FirstOrDefault();
            if (existingTournament is not null)
                return Conflict("Playoffs already exist for this year.");

            var addedTournament = new Tournament { SeasonID = existing.ID };
            dbContext.Tournaments.Add(addedTournament);
            await dbContext.SaveChangesAsync();

            return Ok(new { seasonID = existing.ID, tournamentID = addedTournament.ID });
        }

        var season = new Season
        {
            Year = DateTime.Now.Year,
            Subseason = "Playoffs",
            StartDate = DateTime.Now.Date,
        };
        dbContext.Seasons.Add(season);
        await dbContext.SaveChangesAsync();

        var tournament = new Tournament { SeasonID = season.ID };
        dbContext.Tournaments.Add(tournament);
        await dbContext.SaveChangesAsync();

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Executive/Season/Playoffs", "Created playoffs season",
            new { season.ID, season.Year, TournamentID = tournament.ID }));
        await dbContext.SaveChangesAsync();

        return Ok(new { seasonID = season.ID, tournamentID = tournament.ID });
    }

    [HttpPatch("Season/StartDate")]
    public async Task<IActionResult> UpdateSeasonStartDate([FromBody] SeasonStartDateDto dto)
    {
        var season = await dbContext.Seasons
            .FirstOrDefaultAsync(s => s.Subseason == "Regular Season" && s.Year == DateTime.Now.Year);

        if (season is null)
            return NotFound("No regular season found for this year.");

        if (!DateTime.TryParse(dto.StartDate, out var parsed))
            return BadRequest("Invalid date format.");

        season.StartDate = parsed;
        await dbContext.SaveChangesAsync();

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Executive/Season/StartDate", "Updated season start date",
            new { season.ID, season.Year, StartDate = season.StartDate.ToString("yyyy-MM-dd") }));
        await dbContext.SaveChangesAsync();

        return Ok(new { startDate = season.StartDate.ToString("yyyy-MM-dd") });
    }

    [HttpPatch("Status/{entity}/{id:long}")]
    public async Task<IActionResult> ToggleStatus([FromRoute] string entity, [FromRoute] long id)
    {
        object? affected = null;
        switch (entity.ToLower())
        {
            case "team":
                var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == id);
                if (team is null) return NotFound($"Team {id} not found.");
                team.Active = !team.Active;
                affected = new { team.ID, team.Name, team.Active };
                break;

            case "park":
                var park = await dbContext.Locations.FirstOrDefaultAsync(l => l.ID == id);
                if (park is null) return NotFound($"Park {id} not found.");
                park.Active = !park.Active;
                affected = new { park.ID, park.Name, park.Active };
                break;

            default:
                return BadRequest("Can only toggle status of 'team' or 'park' entities.");
        }

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);

        if (await dbContext.SaveChangesAsync() == 0)
        {
            dbContext.Events.Add(Event.Log(
                EventType.Error, uid,
                $"/api/Executive/Status/{entity}/{id}", "Status toggle failed — no rows written",
                affected));
            await dbContext.SaveChangesAsync();
            return StatusCode(500, "Error updating in the database.");
        }

        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            $"/api/Executive/Status/{entity}/{id}", $"Toggled {entity} status",
            affected));
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// League standings rules: the current per-tenant config (defaults filled
    /// in when unset) plus the comparator registry the UI offers as choices.
    /// Standings rules are league policy, so they live here with the other
    /// league settings rather than under site configuration.
    /// </summary>
    [HttpGet("StandingsRules")]
    public async Task<IActionResult> StandingsRules()
    {
        var siteConfig = await dbContext.SiteConfigs.AsNoTracking().FirstOrDefaultAsync();
        return Ok(new
        {
            standings = StandingsConfigService.Parse(siteConfig?.StandingsJson),
            comparators = StandingsComparators.All
                .Select(c => new { c.Name, c.Description, c.GroupRestricted })
        });
    }

    [HttpPut("StandingsRules")]
    public async Task<IActionResult> UpdateStandingsRules([FromBody] StandingsConfig dto)
    {
        var problems = StandingsConfigService.Validate(dto);
        if (problems.Count > 0)
            return BadRequest(string.Join(" ", problems));

        var siteConfig = await dbContext.SiteConfigs.FirstOrDefaultAsync();
        if (siteConfig is null)
            return StatusCode(500, "Site configuration row is missing.");

        siteConfig.StandingsJson = StandingsConfigService.Serialize(dto);
        await dbContext.SaveChangesAsync();

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Executive/StandingsRules", "Updated standings rules",
            dto));
        await dbContext.SaveChangesAsync();

        return Ok(dto);
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

    /// <summary>
    /// Parses an uploaded xlsx schedule and returns a preview (does not write to DB).
    /// </summary>
    [HttpPost("Schedule/Preview")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    public async Task<IActionResult> SchedulePreview(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var season = await dbContext.Seasons
            .FirstOrDefaultAsync(s => s.Subseason == "Regular Season" && s.Year == DateTime.Now.Year);
        if (season is null)
            return BadRequest("No regular season exists for the current year.");

        var existingGames = await dbContext.Games
            .Where(g => g.SeasonID == season.ID && g.Status!.Name != "Deleted")
            .CountAsync();
        if (existingGames > 0)
            return BadRequest($"Schedule already has {existingGames} games. Import is only available for empty schedules.");

        try
        {
            using var stream = file.OpenReadStream();
            var preview = await scheduleImportService.ParseAsync(stream, season.ID);
            return Ok(preview);
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to parse spreadsheet: {ex.Message}");
        }
    }

    /// <summary>
    /// Confirms and commits the games from a previously previewed schedule import.
    /// </summary>
    [HttpPost("Schedule/Import")]
    public async Task<IActionResult> ScheduleImport([FromBody] ScheduleImportConfirmDto dto)
    {
        if (dto.Games is null || dto.Games.Count == 0)
            return BadRequest("No games provided.");

        var season = await dbContext.Seasons
            .FirstOrDefaultAsync(s => s.ID == dto.SeasonID);
        if (season is null) return NotFound("Season not found.");

        var existingGames = await dbContext.Games
            .Where(g => g.SeasonID == season.ID && g.Status!.Name != "Deleted")
            .CountAsync();
        if (existingGames > 0)
            return BadRequest($"Schedule already has {existingGames} games. Cannot import into non-empty schedule.");

        var upcomingStatus = await dbContext.GameStatuses.FirstAsync(s => s.Name == "Upcoming");

        var games = dto.Games.Select(g => new Game
        {
            SeasonID = season.ID,
            Date = g.Date,
            HostTeamID = g.HostTeamID,
            VisitingTeamID = g.VisitingTeamID,
            LocationID = g.LocationID,
            StatusID = upcomingStatus.ID,
            ScoreHost = null,
            ScoreVisitor = null
        }).ToList();

        await dbContext.Games.AddRangeAsync(games);
        await dbContext.SaveChangesAsync();

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Executive/Schedule/Import", $"Imported {games.Count} games",
            new { season.ID, season.Year, Count = games.Count }));
        await dbContext.SaveChangesAsync();

        return Ok(new { count = games.Count });
    }
}

public record SeasonStartDateDto(string StartDate);

public record ScheduleImportConfirmDto(
    long SeasonID,
    List<ScheduleImportGameConfirm> Games
);

public record ScheduleImportGameConfirm(
    DateTime Date,
    long HostTeamID,
    long VisitingTeamID,
    long LocationID
);
