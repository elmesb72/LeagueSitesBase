using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBackend.Pages;

public class RecycleBinNewsModel(LeagueSitesContext context) : PageModel
{
    public required IEnumerable<News> News { get; set; }
    public required PermissionsManager Permissions { get; set; }

    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync()
    {
        Permissions = await PermissionsManager.CreateAsync(User, dbContext);
        if (Permissions.User == null)
        {
            return RedirectToPage("/Index");
        }

        News = await dbContext.News
            .Include(n => n.Author)
                .ThenInclude(u => u!.UserLogins)
            .Include(n => n.Author)
                .ThenInclude(u => u!.Invitations)
                    .ThenInclude(i => i.Team)
            .Where(n => n.IsDeleted && n.AuthorID == Permissions.User.ID)
            .OrderByDescending(n => n.Date)
            .ToListAsync();

        if (!News.Any())
        {
            return RedirectToPage("/User");
        }

        return Page();
    }

}