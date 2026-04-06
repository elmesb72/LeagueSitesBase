using System.Security.Claims;

public interface IPermissionsService
{
    Task<PermissionsManager> GetAsync(ClaimsPrincipal? user, List<Team>? teams = null);
}

public class PermissionsService(LeagueSitesContext dbContext) : IPermissionsService
{
    readonly LeagueSitesContext dbContext = dbContext;

    public Task<PermissionsManager> GetAsync(ClaimsPrincipal? user, List<Team>? teams = null) =>
        PermissionsManager.CreateAsync(user, dbContext, teams);
}
