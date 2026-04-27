using Facet.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Teams")]
public class APITeamsController(LeagueSitesContext context, ISeasonService seasonService, IPermissionsService permissionsService) : ControllerBase
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

        // Standings for record
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
}
