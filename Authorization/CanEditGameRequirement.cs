using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

public class CanEditGameRequirement : IAuthorizationRequirement;

public class CanEditGameHandler(
    LeagueSitesContext dbContext,
    IPermissionsService permissionsService) : AuthorizationHandler<CanEditGameRequirement, Game>
{
    readonly LeagueSitesContext dbContext = dbContext;
    readonly IPermissionsService permissionsService = permissionsService;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CanEditGameRequirement requirement,
        Game resource)
    {
        if (context.User?.Identity is null || !context.User.Identity.IsAuthenticated)
        {
            return;
        }

        // Ensure HostTeam / VisitingTeam are available for team-scoped permission checks.
        if (resource.HostTeam is null && resource.HostTeamID != 0)
        {
            resource.HostTeam = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == resource.HostTeamID);
        }
        if (resource.VisitingTeam is null && resource.VisitingTeamID != 0)
        {
            resource.VisitingTeam = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == resource.VisitingTeamID);
        }

        var teams = new List<Team>();
        if (resource.HostTeam != null) teams.Add(resource.HostTeam);
        if (resource.VisitingTeam != null) teams.Add(resource.VisitingTeam);

        var permissions = await permissionsService.GetAsync(context.User, teams);
        var allowed =
            permissions.Include([PermissionsScope.Webmaster, PermissionsScope.Executive]) ||
            permissions.Include([PermissionsScope.Manager, PermissionsScope.Scorer], resource.HostTeam) ||
            permissions.Include([PermissionsScope.Manager, PermissionsScope.Scorer], resource.VisitingTeam);

        if (allowed)
        {
            context.Succeed(requirement);
        }
    }
}

