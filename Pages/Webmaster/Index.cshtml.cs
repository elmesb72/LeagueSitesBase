using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LeagueSitesBase.Pages;

public class WebmasterModel(LeagueSitesContext context) : PageModel
{
    public User? SiteUser { get; set; }
    public List<string> Permissions { get; set; } = [];

    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync()
    {
        SiteUser = await UserModel.GetSiteUser(User, dbContext);
        if (User.Identity is null || !User.Identity.IsAuthenticated || SiteUser is null)
        {
            return RedirectToPage("/Login");
        }
        Permissions = UserModel.GetUserPermissions(SiteUser);
        if (!Permissions.Any(p => p == "Webmaster"))
        {
            return RedirectToPage("/User");
        }

        return Page();
    }

}