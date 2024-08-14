using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class CreateSeasonModel(LeagueSitesContext context) : PageModel
{
    readonly LeagueSitesContext dbContext = context;

    public required string Result { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!ExecutiveModel.IsAllowed(User, dbContext, out var redirect))
        {
            return RedirectToPage(redirect);
        }

        // Check if season already exists
        var currentSeason = await dbContext.Seasons
            .FirstOrDefaultAsync(s => s.Subseason == "Regular Season" && s.Year == DateTime.Now.Year);

        if (currentSeason is not null)
        {
            Result = "Cannot create a season: season already exists for this year.";
            return Page();
        }

        var newSeason = new Season()
        {
            Year = DateTime.Now.Year,
            Subseason = "Regular Season",
            StartDate = DateTime.Now.Date
        };
        dbContext.Seasons.Add(newSeason);
        var result = await dbContext.SaveChangesAsync();

        if (result == 0)
        {
            Result = "Error writing to database.";
        }
        else
        {
            Result = $"Created new season with ID {newSeason.ID}.";
        }

        return Page();
    }
}
