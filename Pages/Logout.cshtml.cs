using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LeagueSitesBase.Pages;
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity is not null && User.Identity.IsAuthenticated)
        {
            await HttpContext.SignOutAsync();
        }
        return RedirectToPage("/Index", new { });
    }
}