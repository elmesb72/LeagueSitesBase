using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class HistoryModel(LeagueSitesContext context, IConfiguration config) : PageModel
{
    public List<Year> Years = [];

    readonly LeagueSitesContext dbContext = context;
    readonly IConfiguration config = config;

    public async Task<IActionResult> OnGetAsync()
    {
        // Generate from results in database
        var seasons = await dbContext.Seasons
                                    .Include(s => s.Games)
                                        .ThenInclude(g => g.HostTeam)
                                    .Include(s => s.Games)
                                        .ThenInclude(g => g.VisitingTeam)
                                    .Include(s => s.Games)
                                        .ThenInclude(g => g.Status)
                                    .Include(s => s.Tournaments)
                                        .ThenInclude(t => t.Brackets)
                                            .ThenInclude(b => b.Rounds)
                                                .ThenInclude(r => r.Series)
                                                    .ThenInclude(s => s.Games)
                                    .ToArrayAsync();

        Years = [.. seasons.GroupBy(s => s.Year).Select(yg => new Year(yg.Key, [.. yg]))];

        foreach (var year in Years)
        {
            if (year.HasPlayoffs() && year.PlayoffsTournament != null && year.Playoffs != null && year.RegularSeasonStandings != null)
            {
                year.PlayoffsTournament.Populate([.. year.Playoffs.Games], year.RegularSeasonStandings);
            }
        }

        // Add/overwrite any specified in appsettings (config)
        var history = config.GetSection("Site:History").Get<List<ConfigurationYear>>() ?? [];
        if (history.Count != 0)
        {
            var teams = await dbContext.Teams.ToListAsync();

            foreach (var year in history)
            {
                var existingYear = Years.FirstOrDefault(existingYear => existingYear.CalendarYear == year.Year);
                if (existingYear != null) // Overwrite existing year with hardcoded result
                {
                    existingYear.ExceptionYearDescription = year.Result;
                }
                else
                {
                    var knownTeam = teams.FirstOrDefault(team => team.FullName == year.Result);
                    if (knownTeam != null)
                    {
                        Years.Add(new Year(year.Year, knownTeam));
                    }
                    else
                    {
                        Years.Add(new Year(year.Year, year.Result));
                    }
                    
                }
            }
        }
        
        // Sort and then return
        Years = [.. Years.OrderByDescending(y => y.CalendarYear)];
        return Page();
    }
}

record ConfigurationYear(int Year, string Result);