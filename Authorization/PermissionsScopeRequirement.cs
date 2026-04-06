using Microsoft.AspNetCore.Authorization;

public class PermissionsScopeRequirement(params PermissionsScope[] scopes) : IAuthorizationRequirement
{
    public IReadOnlyList<PermissionsScope> Scopes { get; } = scopes;
}

public class PermissionsScopeHandler(IPermissionsService permissionsService) : AuthorizationHandler<PermissionsScopeRequirement>
{
    readonly IPermissionsService permissionsService = permissionsService;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionsScopeRequirement requirement)
    {
        if (context.User?.Identity is null || !context.User.Identity.IsAuthenticated)
        {
            return;
        }

        var permissions = await permissionsService.GetAsync(context.User);
        if (permissions.Include([.. requirement.Scopes]))
        {
            context.Succeed(requirement);
        }
    }
}

