public interface ISeedingConfigurationSource
{
    public Task<IEnumerable<Team>> GetTeamsAsync(LeagueSitesContext dbContext);
}