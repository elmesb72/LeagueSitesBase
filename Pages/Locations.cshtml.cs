using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class LocationsModel(LeagueSitesContext context) : PageModel
{
    public List<Location> Locations { get; set; } = [];
    
    readonly LeagueSitesContext dbContext = context;
    
    public async Task<IActionResult> OnGetAsync()
    {
        Locations = await dbContext.Locations
            .Where(p => p.Active)
            .Include(p => p.Games)
                .ThenInclude(g => g.Status)
            .Include(p => p.Games)
                .ThenInclude(g => g.HostTeam)
            .Include(p => p.Games)
                .ThenInclude(g => g.VisitingTeam)
            .OrderBy(p => p.City)
            .ThenBy(p => p.Name)
            .ToListAsync();
        return Page();
    }
}