using Microsoft.AspNetCore.Authorization;

public static class AuthorizationServiceExtensions
{
    public static IServiceCollection AddLeagueSitesAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization();

        services.AddSingleton<IAuthorizationPolicyProvider, ScopePolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionsScopeHandler>();
        services.AddScoped<IAuthorizationHandler, TeamScopedHandler>();

        return services;
    }
}
