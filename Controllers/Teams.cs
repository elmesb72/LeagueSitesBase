using Facet.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Teams")]
public class APITeamsController(LeagueSitesContext context, ISeasonService seasonService, IPermissionsService permissionsService, IAuthorizationService authorizationService) : ControllerBase
{
    readonly LeagueSitesContext dbContext = context;

    [ResponseCache(Duration = 30)]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var teams = await dbContext.Teams
            .Where(t => t.Active)
            .OrderBy(t => t.Name)
            .SelectFacet<TeamDetailDto>()
            .ToListAsync();

        return Ok(teams);
    }

    [ResponseCache(Duration = 30)]
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get([FromRoute] long id)
    {
        var team = await dbContext.Teams
            .Where(t => t.ID == id)
            .SelectFacet<TeamDetailDto>()
            .FirstOrDefaultAsync();

        if (team is null)
            return NotFound();

        return Ok(team);
    }

    [HttpGet("{abbreviation}/Page")]
    public async Task<IActionResult> GetTeamPage([FromRoute] string abbreviation, [FromQuery] int? year)
    {
        var team = await dbContext.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Abbreviation.ToLower() == abbreviation.ToLower());

        if (team is null)
            return NotFound();

        // Resolve season
        Season? season;
        if (year != null)
        {
            season = await dbContext.Seasons
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Year == year && s.Subseason == "Regular Season");
        }
        else
        {
            season = await seasonService.GetClosestSeasonAsync();
        }

        // Schedule games for this team in the resolved season
        var games = new List<Game>();
        if (season != null)
        {
            games = await dbContext.Games
                .AsNoTracking()
                .Include(g => g.HostTeam)
                .Include(g => g.VisitingTeam)
                .Include(g => g.Location)
                .Include(g => g.Status)
                .Include(g => g.Season)
                .Where(g => g.Season!.Year == season.Year
                    && g.Status!.Name != "Deleted"
                    && (g.HostTeamID == team.ID || g.VisitingTeamID == team.ID))
                .ToListAsync();

            games = [.. games.OrderBy(g => g.Date)];
        }

        // Active roster
        var activeRoster = await dbContext.Invitations
            .AsNoTracking()
            .Include(i => i.Player)
            .Include(i => i.Status)
            .Include(i => i.InvitationRoles)
                .ThenInclude(ir => ir.Role)
            .Include(i => i.User)
                .ThenInclude(u => u!.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .Include(i => i.InvitationEmails)
            .Where(i => i.TeamID == team.ID && i.PlayerID != null && i.Status!.Name == "Active")
            .OrderBy(i => i.Player!.LastName)
            .ThenBy(i => i.Player!.FirstName)
            .ToListAsync();

        // Managers
        var managers = await dbContext.Invitations
            .AsNoTracking()
            .Include(i => i.Player)
            .Include(i => i.User)
                .ThenInclude(u => u!.UserLogins)
            .Include(i => i.InvitationRoles)
                .ThenInclude(ir => ir.Role)
            .Include(i => i.InvitationEmails)
            .Where(i => i.TeamID == team.ID
                && i.InvitationRoles.Any(ir => ir.Role!.Name == "Manager"))
            .ToListAsync();

        var managerNames = managers.Select(m =>
        {
            if (m.Player != null) return m.Player.Name;
            if (m.User != null) return m.User.UserLogins.FirstOrDefault(ul => ul.IsPrimary)?.Name ?? "Unknown";
            return $"Pending ({m.InvitationEmails.FirstOrDefault()?.Email ?? "Unknown"})";
        }).ToList();

        // Standings for record. Default rules deliberately: only the W-L-T
        // record string is read, which no configured rule can change (order
        // and point values are unused, and W/L/T counts don't depend on the
        // forfeit score values). See configurable-standings-rules Req 4.2.
        var standings = new Standings(games.Where(g => g.Status?.Name == "Played").ToList());
        string record = standings.ContainsKey(team) ? standings[team].ToString() : "(0-0)";

        // Permissions
        var permissions = await permissionsService.GetAsync(User, [team]);
        var canAddPlayer = permissions.Allow("CreateGame"); // Scorer, Manager, Executive, Webmaster
        var isTeamMember = permissions.Include([PermissionsScope.Manager, PermissionsScope.Scorer, PermissionsScope.Reporter], team)
            || permissions.Include([PermissionsScope.Executive, PermissionsScope.Webmaster]);
        var canEditTeam = canAddPlayer;
        var canViewAdminIcons = permissions.Include([PermissionsScope.Manager], team)
            || permissions.Include([PermissionsScope.Executive, PermissionsScope.Webmaster]);

        // Inactive roster (only for team members / executives / webmasters)
        object? inactiveRoster = null;
        if (isTeamMember)
        {
            var substitutes = await dbContext.Invitations
                .AsNoTracking()
                .Include(i => i.Player)
                .Include(i => i.Status)
                .Include(i => i.InvitationRoles).ThenInclude(ir => ir.Role)
                .Include(i => i.User).ThenInclude(u => u!.UserRoles).ThenInclude(ur => ur.Role)
                .Where(i => i.TeamID == team.ID && i.PlayerID != null && i.Status!.Name == "Substitute")
                .OrderBy(i => i.Player!.LastName).ThenBy(i => i.Player!.FirstName)
                .ToListAsync();

            var formerPlayers = await dbContext.Invitations
                .AsNoTracking()
                .Include(i => i.Player)
                .Include(i => i.Status)
                .Include(i => i.InvitationRoles).ThenInclude(ir => ir.Role)
                .Include(i => i.User).ThenInclude(u => u!.UserRoles).ThenInclude(ur => ur.Role)
                .Where(i => i.TeamID == team.ID && i.PlayerID != null
                    && (i.Status!.Name == "Retired" || i.Status.Name == "Other"))
                .OrderBy(i => i.Player!.LastName).ThenBy(i => i.Player!.FirstName)
                .ToListAsync();

            var nonPlayerUsers = await dbContext.Invitations
                .AsNoTracking()
                .Include(i => i.Player)
                .Include(i => i.Status)
                .Include(i => i.InvitationRoles).ThenInclude(ir => ir.Role)
                .Include(i => i.User).ThenInclude(u => u!.UserLogins)
                .Include(i => i.User).ThenInclude(u => u!.UserRoles).ThenInclude(ur => ur.Role)
                .Include(i => i.InvitationEmails)
                .Where(i => i.TeamID == team.ID && i.PlayerID == null && i.Status!.Name != "Hidden")
                .ToListAsync();

            Func<Invitation, object> mapInvitation = i => new
            {
                id = i.ID,
                player = i.Player != null ? new PlayerSummaryDto(i.Player) : null,
                userName = i.User?.UserLogins.FirstOrDefault(ul => ul.IsPrimary)?.Name,
                email = i.InvitationEmails.FirstOrDefault()?.Email,
                roles = i.InvitationRoles.Select(ir => ir.Role!.Name),
                userRoles = i.User?.UserRoles.Select(ur => ur.Role!.Name) ?? [],
                hasUser = i.User != null,
                hasInvitationEmails = i.InvitationEmails?.Count > 0
            };

            inactiveRoster = new
            {
                substitutes = substitutes.Select(mapInvitation),
                formerPlayers = formerPlayers.Select(mapInvitation),
                nonPlayerUsers = nonPlayerUsers.Select(mapInvitation)
            };
        }

        return Ok(new
        {
            team = new TeamDetailDto(team),
            season = season != null ? new SeasonSummaryDto(season) : null,
            record,
            managers = managerNames,
            games = games.Select(g => new GameSummaryDto(g)),
            roster = activeRoster.Select(i => new
            {
                id = i.ID,
                player = i.Player != null ? new PlayerSummaryDto(i.Player) : null,
                roles = i.InvitationRoles.Select(ir => ir.Role!.Name),
                userRoles = i.User?.UserRoles.Select(ur => ur.Role!.Name) ?? [],
                hasUser = i.User != null,
                hasInvitationEmails = i.InvitationEmails?.Count > 0
            }),
            canAddPlayer,
            canEditTeam,
            isTeamMember,
            canViewAdminIcons,
            inactiveRoster
        });
    }

    [Authorize(Policy = "Scope:Scorer,Manager,Executive,Webmaster")]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateTeam([FromRoute] long id, [FromBody] TeamUpdateDto dto)
    {
        var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == id);
        if (team is null) return NotFound();

        var auth = await authorizationService.AuthorizeAsync(
            User, team,
            new TeamScopedRequirement(
                PermissionsScope.Manager, PermissionsScope.Executive, PermissionsScope.Webmaster));
        if (!auth.Succeeded) return Forbid();

        team.Location = dto.Location;
        team.Name = dto.Name;
        team.Abbreviation = dto.Abbreviation;
        team.BackgroundColor = dto.BackgroundColor;
        team.Color = dto.Color;

        dbContext.Teams.Update(team);
        await dbContext.SaveChangesAsync();

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Teams/" + id, "Updated team",
            new TeamDetailDto(team)));
        await dbContext.SaveChangesAsync();

        return Ok(new TeamDetailDto(team));
    }

    [Authorize(Policy = "Scope:Executive,Webmaster")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TeamUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Location) || string.IsNullOrWhiteSpace(dto.Name)
            || string.IsNullOrWhiteSpace(dto.Abbreviation))
            return BadRequest("Location, name, and abbreviation are required.");

        if (!IsHex6(dto.BackgroundColor) || !IsHex6(dto.Color))
            return BadRequest("BackgroundColor and Color must be 6-character hex strings (e.g. 'FFFFFF').");

        var duplicate = await dbContext.Teams
            .AnyAsync(t => t.Abbreviation.ToLower() == dto.Abbreviation.ToLower());
        if (duplicate)
            return Conflict($"A team with abbreviation '{dto.Abbreviation}' already exists.");

        var team = new Team
        {
            Location = dto.Location,
            Name = dto.Name,
            Abbreviation = dto.Abbreviation,
            BackgroundColor = dto.BackgroundColor,
            Color = dto.Color,
            Active = true,
            Hidden = false
        };

        await dbContext.Teams.AddAsync(team);
        await dbContext.SaveChangesAsync();

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Teams", "Created team",
            new TeamDetailDto(team)));
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = team.ID }, new TeamDetailDto(team));
    }

    [Authorize(Policy = "Scope:Executive,Webmaster")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete([FromRoute] long id)
    {
        var team = await dbContext.Teams
            .Include(t => t.GameHostTeam)
            .Include(t => t.GameVisitingTeam)
            .Include(t => t.Invitations)
            .Include(t => t.Socials)
            .FirstOrDefaultAsync(t => t.ID == id);

        if (team is null) return NotFound();

        var blockers = new List<string>();
        if (team.GameHostTeam.Count > 0 || team.GameVisitingTeam.Count > 0)
            blockers.Add($"{team.GameHostTeam.Count + team.GameVisitingTeam.Count} game(s)");
        if (team.Invitations.Count > 0)
            blockers.Add($"{team.Invitations.Count} invitation(s)");
        if (team.Socials.Count > 0)
            blockers.Add($"{team.Socials.Count} social link(s)");

        if (blockers.Count > 0)
            return Conflict($"Cannot delete team — it has {string.Join(", ", blockers)}.");

        var snapshot = new TeamDetailDto(team);
        dbContext.Teams.Remove(team);
        await dbContext.SaveChangesAsync();

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Teams/" + id, "Deleted team",
            snapshot));
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// Returns a dictionary of names and jersey numbers for active players on the given team.
    /// Optional query parameter: exclude (string) removes a player matching the provided number.
    [ResponseCache(Duration = 30)]
    [HttpGet("{id:long}/Players")]
    public async Task<IActionResult> GetPlayers([FromRoute] long id, [FromQuery] string? exclude)
    {
        var players = await dbContext.Invitations
            .AsNoTracking()
            .Include(i => i.Status)
            .Where(i => i.Status!.Name == "Active")
            .Where(i => i.TeamID == id)
            .Where(i => i.PlayerID != null)
            .Select(i => i.Player!)
            .Where(p => !string.IsNullOrEmpty(p.Number))
            .ToListAsync();

        if (!string.IsNullOrEmpty(exclude))
            players = players.Where(p => p.Number != exclude).ToList();

        return Ok(players.ToDictionary(p => p.Number!, p => p.Name));
    }

    static bool IsHex6(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length == 6
        && System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Fa-f0-9]{6}$");
}
