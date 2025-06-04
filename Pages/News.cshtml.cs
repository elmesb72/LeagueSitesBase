using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class NewsModel(LeagueSitesContext context) : PageModel
{
    public required PermissionsManager Permissions { get; set; }
    [BindProperty]
    public NewsForm? Form { get; set; }
    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        Permissions = await PermissionsManager.CreateAsync(User, dbContext);
        if (!Permissions.Allow("Post"))
        {
            return RedirectToPage("/Index");
        }

        if (id != null)
        {
            var news = await dbContext.News
                .Include(n => n.Author)
                    .ThenInclude(u => u!.Invitations)
                        .ThenInclude(i => i.Team)
                .FirstOrDefaultAsync(n => n.ID == id);
            if (news == null)
            {
                return RedirectToPage("/Index");
            }
            Form = new NewsForm(news);
        }
        else
        {
            Form = new NewsForm(Permissions.User!);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long? id)
    {
        Permissions = await PermissionsManager.CreateAsync(User, dbContext);
        if (!Permissions.Allow("Post"))
        {
            return RedirectToPage("/Index");
        }

        if (id != null)
        {
            var news = await dbContext.News.FirstOrDefaultAsync(n => n.ID == id);
            if (news == null)
            {
                return RedirectToPage("/Index");
            }
            Form!.UpdateExistingNewsPost(ref news);

            dbContext.News.Update(news);
            await dbContext.SaveChangesAsync();
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(Form!.Title) && !string.IsNullOrWhiteSpace(Form!.Contents))
            {
                var news = Form.ToNews(Permissions.User!.ID);
                await dbContext.News.AddAsync(news);
                await dbContext.SaveChangesAsync();
                return RedirectToPage("/Index");
            }
            return Page();
        }
        return RedirectToPage("/Index");
    }   
}