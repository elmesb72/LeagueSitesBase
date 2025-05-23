using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class PlayerModel(LeagueSitesContext context) : PageModel
{
    public Player? Player { get; set; }
    public Player? ReferredBy { get; set; }

    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync(string code)
    {
        Player = await dbContext.Players
            .Include(p => p.Invitations)
                .ThenInclude(i => i.Team)
            .Include(p => p.Invitations)
                .ThenInclude(i => i.User)
            .Include(p => p.Bio)
            .Include(p => p.Socials)
                .ThenInclude(s => s.Platform)
            .FirstOrDefaultAsync(p => p.ShortCode == code);
        if (Player == null)
        {
            return Redirect("/");
        }

        if (Player.Bio != null && !string.IsNullOrEmpty(Player.Bio.ReferredBy))
        {
            ReferredBy = await dbContext.Players.FirstOrDefaultAsync(p => p.FirstName + " " + p.LastName == Player.Bio.ReferredBy);
        }

        return Page();
    }
}