using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class GameModel(LeagueSitesContext context) : PageModel
{
    public enum PageMode
    {
        View,
        Edit,
        EditNew,
    }
    public PageMode Mode { get; set; } = PageMode.View;

    [BindProperty]
    public Game? Game { get; set; }
    public Standings? Records { get; set; }
    public User? SiteUser { get; set; }
    public List<string> Permissions { get; set; } = [];

    public LeagueSitesContext DBContext { get; set; } = context;

    public async Task<IActionResult> OnGetAsync(int id, string? mode)
    {
        if (!string.IsNullOrEmpty(mode))
        {
            if (Enum.TryParse(mode, out PageMode pageMode))
            {
                Mode = pageMode;
            }
            else
            {
                return RedirectToPage("/Game", new { id = id });
            }
        }

        Game = await DBContext.Games
            .Include(g => g.Status)
            .Include(g => g.Season)
            .Include(g => g.Location)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .FirstOrDefaultAsync(g => id == g.ID);

        if (Game == default)
        {
            return RedirectToPage("/Index");
        }

        var games = await DBContext.Games
            .Where(g => g.Status!.Name != "Deleted")
            .Where(g => g.SeasonID == Game.SeasonID && g.Date <= Game.Date)
            .Include(g => g.Status)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .OrderBy(g => g.Date)
            .ToListAsync();

        /* If no games exist on a date equal to or before this, the above set returns empty.
            An empty set of games means no there are no teams in the following Standings calculation.
            No teams in the standings means KeyNotFound exception displaying their 0-0 records. */
        if (!games.Any(g => g.ID == Game.ID))
        {
            games.Add(Game);
        }
        Records = new Standings(games);

        SiteUser = await UserModel.GetSiteUser(User, DBContext);
        if (SiteUser != null)
        {
            Permissions = UserModel.GetUserPermissions(SiteUser, [Game.HostTeam, Game.VisitingTeam]);
        }

        if (Mode == PageMode.Edit && !Permissions.Any(p => new List<string>
            {
                "Webmaster",
                "Executive",
                Game.HostTeam?.Abbreviation + "-Manager",
                Game.HostTeam?.Abbreviation + "-Scorer",
                Game.VisitingTeam?.Abbreviation + "-Manager",
                Game.VisitingTeam?.Abbreviation + "-Scorer",
            }
            .Contains(p)))
        {
            return RedirectToPage("/Game", new { id = id });
        }
        if (Mode == PageMode.EditNew && !Permissions.Any(p => new List<string>
            {
                "Webmaster",
                "Executive",
            }
            .Contains(p)))
        {
            return RedirectToPage("/Game", new { id = id, mode = "Edit" });
        }

        return Page();
    }
    
    public async Task<IActionResult> OnPostAsync(int id, string? mode)
    {
        if (Game == null)
        {
            return Redirect("/Index");
        }
        SiteUser = await UserModel.GetSiteUser(User, DBContext);
        // TODO - confirm user can't access this without permissions

        var status = DBContext.GameStatuses.First(gs => gs.ID == Game!.StatusID).Name;
        if (status != "Played")
        {
            Game!.ScoreHost = null;
            Game.ScoreVisitor = null;
        }
        DBContext.Games.Update(Game);
        DBContext.Events.Add(Event.Log(EventType.Update, SiteUser?.ID ?? -1, "/Game/Edit/" + id, "Updated game", JsonConvert.SerializeObject(Game)));
        await DBContext.SaveChangesAsync();

        /*// Email user
        await DBContext.Entry(Game).Reference(g => g.HostTeam).LoadAsync();
        await DBContext.Entry(Game).Reference(g => g.VisitingTeam).LoadAsync();

        if (!env.IsDevelopment())
        {
            var response = await EmailHelper.SendEmail(
                config["APIKeys:SendGrid"], 
                new List<EmailAddress> { new EmailAddress("elmesb72@gmail.com") }, 
                EmailType.GameUpdate,
                new { status, Game });
        }*/

        return RedirectToPage("/Game", new { id = id });
    }

}