using Microsoft.AspNetCore.Authorization;

public static class AuthorizationServiceExtensions
{
    public static IServiceCollection AddLeagueSitesAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy("ExecutiveOrWebmaster", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionsScopeRequirement(PermissionsScope.Executive, PermissionsScope.Webmaster));
            })
            .AddPolicy("CanEditGame", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new CanEditGameRequirement());
            });

        services.AddScoped<IAuthorizationHandler, CanEditGameHandler>();
        services.AddScoped<IAuthorizationHandler, PermissionsScopeHandler>();

        return services;
    }
}
