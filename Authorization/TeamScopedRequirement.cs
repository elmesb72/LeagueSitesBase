using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Requires the user to have at least one of the specified permission scopes,
/// checked at site level first, then against each team provided by the resource.
/// The resource must implement ITeamScoped.
/// </summary>
public class TeamScopedRequirement(params PermissionsScope[] scopes) : IAuthorizationRequirement
{
    public IReadOnlyList<PermissionsScope> Scopes { get; } = scopes;
}

public class TeamScopedHandler(
    IPermissionsService permissionsService,
    LeagueSitesContext dbContext)
    : AuthorizationHandler<TeamScopedRequirement, ITeamScoped>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TeamScopedRequirement requirement,
        ITeamScoped resource)
    {
        if (context.User?.Identity is null || !context.User.Identity.IsAuthenticated)
            return;

        // Collect teams that were already loaded via Include().
        // For any FK IDs that don't have a corresponding loaded team,
        // load them from the DB so the auth check is never silently skipped
        // due to a missing Include() in the calling query.
        var loadedTeams = resource.GetRelatedTeams()
            .Where(t => t != null)
            .Cast<Team>()
            .ToList();
        var loadedIds = loadedTeams.Select(t => t.ID).ToHashSet();
        var missingIds = resource.GetRelatedTeamIds()
            .Where(id => !loadedIds.Contains(id))
            .Distinct()
            .ToList();

        if (missingIds.Count > 0)
        {
            var loaded = await dbContext.Teams
                .Where(t => missingIds.Contains(t.ID))
                .ToListAsync();
            loadedTeams.AddRange(loaded);
        }

        var permissions = await permissionsService.GetAsync(context.User, loadedTeams);

        // Site-level check
        if (permissions.Include([.. requirement.Scopes]))
        {
            context.Succeed(requirement);
            return;
        }

        // Team-level check
        foreach (var team in loadedTeams)
        {
            if (permissions.Include([.. requirement.Scopes], team))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
