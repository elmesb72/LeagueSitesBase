using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class EditEntityStatusModel(LeagueSitesContext context) : PageModel
{
    readonly LeagueSitesContext dbContext = context;

    public string? Entity { get; set; }
    public string? Result { get; set; }

    public async Task<IActionResult> OnGetAsync(string entity, int id)
    {
        if (!ExecutiveModel.IsAllowed(User, dbContext, out var redirect))
        {
            return RedirectToPage(redirect);
        }

        Entity = entity.ToLower();
        if (Entity == "team")
        {
            var team = await dbContext.Teams.FirstOrDefaultAsync(team => team.ID == id);
            if (team == null)
            {
                Result = $"Cannot edit team ID {id}: not found.";
                return Page();
            }
            else
            {
                team.Active = !team.Active;
                if (await dbContext.SaveChangesAsync() == 0)
                {
                    Result = "Error updating in the database.";
                }
                else
                {
                    return RedirectToPage("../Index", new { }); // Success
                }
            }
        }
        else if (Entity == "park")
        {
            var park = await dbContext.Locations.FirstOrDefaultAsync(park => park.ID == id);
            if (park == null)
            {
                Result = $"Cannot edit team ID {id}: not found.";
                return Page();
            }
            else
            {
                park.Active = !park.Active;
                if (await dbContext.SaveChangesAsync() == 0)
                {
                    Result = "Error updating in the database.";
                    return Page();
                }
                else
                {
                    return RedirectToPage("../Index", new { }); // Success
                }
            }
        }
        Result = "Can only edit status of Team or Park entities.";
        return Page();
    }
}