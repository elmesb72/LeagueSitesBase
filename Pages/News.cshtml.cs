using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class NewsModel(LeagueSitesContext context) : PageModel
{
    public required PermissionsManager Permissions { get; set; }
    public News? News { get; set; }
    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        Permissions = await PermissionsManager.Create(User, dbContext);
        if (!Permissions.Allow("Post"))
        {
            return RedirectToPage("/Index");
        }

        if (id != null)
        {
            News = await dbContext.News.FirstOrDefaultAsync(n => n.ID == id);
            if (News == null)
            {
                return RedirectToPage("/Index");
            }
        }

        return Page();
    }
}