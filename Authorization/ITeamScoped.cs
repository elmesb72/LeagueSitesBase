/// <summary>
/// Implemented by any resource that has associated teams,
/// enabling team-scoped authorization checks.
/// </summary>
public interface ITeamScoped
{
    /// <summary>
    /// Returns the foreign key IDs for related teams.
    /// These are always available on the entity, even without Include().
    /// </summary>
    IEnumerable<long> GetRelatedTeamIds();

    /// <summary>
    /// Returns the loaded navigation properties for related teams.
    /// May contain nulls if the teams weren't loaded via Include().
    /// </summary>
    IEnumerable<Team?> GetRelatedTeams();
}
