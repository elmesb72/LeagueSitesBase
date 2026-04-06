using Microsoft.EntityFrameworkCore;

public interface ISeasonService
{
    Task<Season?> GetClosestSeasonAsync(DateTime? relativeTo = null);
}

public class SeasonService(LeagueSitesContext dbContext) : ISeasonService
{
    public async Task<Season?> GetClosestSeasonAsync(DateTime? relativeTo = null)
    {
        var seasons = await dbContext.Seasons
            .Where(s => s.Subseason == "Regular Season")
            .ToListAsync();

        var closest = seasons.FirstOrDefault();
        if (closest is null) return null;

        var target = relativeTo ?? DateTime.Today;
        foreach (var s in seasons)
        {
            if (target.Subtract(s.StartDate).Duration() < target.Subtract(closest.StartDate).Duration())
                closest = s;
        }
        return closest;
    }
}
