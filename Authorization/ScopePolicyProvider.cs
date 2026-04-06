using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

/// <summary>
/// Dynamically resolves policies named "Scope:Executive,Webmaster" (etc.)
/// so you don't need to pre-register every combination via AddPolicy.
/// Falls back to the default provider for any other policy name.
/// </summary>
public class ScopePolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    const string Prefix = "Scope:";
    readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix))
            return _fallback.GetPolicyAsync(policyName);

        var scopes = policyName[Prefix.Length..]
            .Split(',')
            .Select(s => Enum.Parse<PermissionsScope>(s.Trim()))
            .ToArray();

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionsScopeRequirement(scopes))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallback.GetFallbackPolicyAsync();
}
