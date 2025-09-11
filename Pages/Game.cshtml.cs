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
        Create,
    }
    public PageMode Mode { get; set; }

    [BindProperty]
    public Game? Game { get; set; }
    public Standings? Records { get; set; }
    public required PermissionsManager Permissions { get; set; }

    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync(int? id, [FromQuery] string? date, [FromQuery] int? location, [FromQuery] int? seriesGame, [FromQuery] int? roundRobinGame)
    {
        Permissions = await PermissionsManager.CreateAsync(User, dbContext);
        if (id == null)
        {
            if (Permissions.Allow("CreateGame"))
            {
                var dateIncluded = DateTime.TryParse(date, out var parsedDate);
                Game = new Game()
                {
                    ID = -1,
                    Date = dateIncluded ? parsedDate : DateTime.Today,
                    LocationID = location ?? 0,
                    StatusID = 1,
                };
                Mode = PageMode.Create;
            }
            else
            {
                return RedirectToPage("/Index");
            }
        }
        else
        {
            Mode = PageMode.View;

            Game = await dbContext.Games
            .Include(g => g.Status)
            .Include(g => g.Season)
            .Include(g => g.Location)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .FirstOrDefaultAsync(g => id == g.ID);

            if (Game == null)
            {
                return RedirectToPage("/Index");
            }
            Records = await GetRecords();

            if (Permissions.Include([PermissionsScope.Webmaster, PermissionsScope.Executive]) ||
                Permissions.Include([PermissionsScope.Manager, PermissionsScope.Scorer], Game.HostTeam) ||
                Permissions.Include([PermissionsScope.Manager, PermissionsScope.Scorer], Game.VisitingTeam))
            {
                Mode = PageMode.Edit;
            }
        }

        return Page();
    }

    public async Task<Standings> GetRecords()
    {
        var games = await dbContext.Games
            .Where(g => g.Status!.Name != "Deleted")
            .Where(g => g.SeasonID == Game!.SeasonID && g.Date <= Game.Date)
            .Include(g => g.Status)
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .OrderBy(g => g.Date)
            .ToListAsync();

        /* If no games exist on a date equal to or before this, the above set returns empty.
            An empty set of games means no there are no teams in the following Standings calculation.
            No teams in the standings means KeyNotFound exception displaying their 0-0 records. */
        if (!games.Any(g => g.ID == Game!.ID))
        {
            games.Add(Game!);
        }
        return new Standings(games);
    }
    
    public async Task<IActionResult> OnPostAsync([FromQuery] int? seriesGame, [FromQuery] int? roundRobinGame)
    {
        if (Game == null)
        {
            return RedirectToPage("/Index");
        }

        List<Team> gameTeams = [];
        if (Game.HostTeam != null)
        {
            gameTeams.Add(Game.HostTeam);
        }
        if (Game.VisitingTeam != null)
        {
            gameTeams.Add(Game.VisitingTeam);
        }
        Permissions = await PermissionsManager.CreateAsync(User, dbContext, gameTeams);
        var siteUser = await UserModel.GetSiteUser(User, dbContext);
        
        var status = (await dbContext.GameStatuses.FirstAsync(gs => gs.ID == Game!.StatusID)).Name;
        if (status != "Played")
        {
            Game!.ScoreHost = null;
            Game.ScoreVisitor = null;
        }

        if (Game.ID == -1) // Create
        {
            Game.ID = 0; // EF Core will ignore 0 in the INSERT so it gets an actual incremented ID
            if (!Permissions.Allow("CreateGame"))
            {
                return RedirectToPage("/Index");
            }

            await dbContext.Games.AddAsync(Game);
            await dbContext.SaveChangesAsync();
            await dbContext.Events.AddAsync(Event.Log(EventType.Update, siteUser?.ID ?? -1, "/Game/" + Game.ID, "Added new game", JsonConvert.SerializeObject(Game)));
            await dbContext.SaveChangesAsync();
        }
        else // Update
        {
            if (!(Permissions.Include([PermissionsScope.Webmaster, PermissionsScope.Executive]) ||
                Permissions.Include([PermissionsScope.Manager, PermissionsScope.Scorer], Game.HostTeam) ||
                Permissions.Include([PermissionsScope.Manager, PermissionsScope.Scorer], Game.VisitingTeam)))
            {
                return RedirectToPage("/Index");
            }

            dbContext.Games.Update(Game);
            dbContext.Events.Add(Event.Log(EventType.Update, siteUser?.ID ?? -1, "/Game/" + Game.ID, "Updated game", JsonConvert.SerializeObject(Game)));
            await dbContext.SaveChangesAsync();
        }

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

        return RedirectToPage("/Game", new { id = Game.ID });
    }

}