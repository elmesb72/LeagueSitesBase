using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Requires the user to have at least one of the specified permission scopes
/// at the site level or on any team.
/// Use for non-resource-based checks (e.g. "can this user post news at all?").
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
        if (permissions.Include([.. requirement.Scopes]))
        {
            context.Succeed(requirement);
        }
    }
}
