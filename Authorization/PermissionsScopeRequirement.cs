using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Requires the user to have at least one of the specified permission scopes
/// at the site level or on any team.
/// Use for gatekeeper policies (e.g. "can this user create games at all?").
/// Follow up with resource-based auth (TeamScopedRequirement) to confirm the
/// user has scope on the specific resource being acted on.
/// </summary>
public class PermissionsScopeRequirement(params PermissionsScope[] scopes) : IAuthorizationRequirement
{
    public IReadOnlyList<PermissionsScope> Scopes { get; } = scopes;
}

public class PermissionsScopeHandler(IPermissionsService permissionsService)
    : AuthorizationHandler<PermissionsScopeRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionsScopeRequirement requirement)
    {
        if (context.User?.Identity is null || !context.User.Identity.IsAuthenticated)
            return;

        var permissions = await permissionsService.GetAsync(context.User);
        if (permissions.IncludeAnyScope([.. requirement.Scopes]))
        {
            context.Succeed(requirement);
        }
    }
}
