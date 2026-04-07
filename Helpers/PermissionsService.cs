using System.Security.Claims;

public interface IPermissionsService
{
    Task<PermissionsManager> GetAsync(ClaimsPrincipal? user, List<Team>? teams = null);
}

/// <summary>
/// Scoped service — lives for one HTTP request. Caches the PermissionsManager
/// so multiple authorization checks in the same request don't re-query the DB.
/// </summary>
public class PermissionsService(LeagueSitesContext dbContext) : IPermissionsService
{
    PermissionsManager? _cached;

    public async Task<PermissionsManager> GetAsync(ClaimsPrincipal? user, List<Team>? teams = null)
    {
        _cached ??= await PermissionsManager.CreateAsync(user, dbContext, teams);
        return _cached;
    }
}
