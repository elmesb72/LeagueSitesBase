
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class TeamModel(LeagueSitesContext context) : PageModel
{
    public List<Team> Teams { get; set; } = [];
    public string? Search { get; set; }
    public string? SearchResults { get; set; }
    public User? SiteUser { get; set; }
    public List<string> Permissions { get; set; } = [];

    public Team? Team { get; set; }
    public Invitation? Manager { get; set; }
    public Schedule? Schedule { get; set; }
    public List<Invitation> ActiveRoster { get; set; } = [];
    public List<Invitation> Substitutes { get; set; } = [];
    public List<Invitation> FormerPlayers { get; set; } = [];
    public List<Invitation> NonPlayerUsers { get; set; } = [];
    public List<string> Managers { get; set; } = [];

    [BindProperty]
    public TeamForm? Form { get; set; }

    readonly LeagueSitesContext dbContext = context;

    public async Task<IActionResult> OnGetAsync(string search, int? year) {
        Search = search;
        await GetData(search, year);

        if (Team != null)
        {
            Form = new TeamForm(Team);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string search, int? year) {
        Search = search;
        await GetData(search, year);

        var team = Team!;
        Form!.UpdateExistingTeam(ref team);
            
        if(Permissions.Any(p => new List<string>() { "Scorer", "Manager", "League Executive", "Webmaster" }.Contains(p))) {
            dbContext.Teams.Update(team);
            //dbContext.Events.Add(Event.Log(EventType.Update, SiteUser!.ID, "/Team/" + search, "Updated team", JsonConvert.SerializeObject(team)));
            await dbContext.SaveChangesAsync();
        }
        return RedirectToPage("/Team", new { search = Form.Abbreviation } );
    }

    async Task GetData(string search, int? year) {
        if (User.Identity!.IsAuthenticated) {
            var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
            SiteUser = await dbContext.Users
            .Include(u => u.Invitations)
                .ThenInclude(i => i.InvitationEmails)
            .Include(u => u.Invitations)
                .ThenInclude(i => i.InvitationRoles)
                    .ThenInclude(r => r.Role)
            .Include(u => u.Invitations)
                .ThenInclude(i => i.Team)
            .Include(u => u.UserLogins)
                .ThenInclude(l => l.LoginSource)
            .Include(u => u.UserRoles)
                .ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.ID == uid);

            Permissions.Add("User");
            Permissions.AddRange(SiteUser?.UserRoles.Select(ur => ur.Role!.Name) ?? []);
            var primaryUserName = SiteUser?.UserLogins.First(ul => ul.IsPrimary).Name;
            if (primaryUserName != null)
            {
                Permissions.Add(primaryUserName);
            }
        }

        if (string.IsNullOrEmpty(search)) {
            // List teams
            SearchResults = "No team provided; showing all teams";
            Teams = await dbContext.Teams.ToListAsync();
        }
        else {
            var teamPossibilities = FindTeams(search);
            if (!teamPossibilities.Any()) {
                // No search results/unknown team
                SearchResults = "No results; showing all teams";
                Teams = await dbContext.Teams.Where(t => !t.Hidden).ToListAsync();
            }
            else if (teamPossibilities.Count() == 1) {
                Team = await GetTeamData(teamPossibilities.First());

                Season? currentSeason = null;
                if (year != null)
                {
                    currentSeason = await dbContext.Seasons.FirstOrDefaultAsync(s => s.Year == (int)year && s.Subseason == "Regular Season");
                }
                currentSeason ??= await IndexModel.GetClosestSeasonAsync(dbContext);
                if (currentSeason == null)
                {
                    return;
                }
                Schedule = new Schedule() {
                    Games = Team.Games.Where(g => g.SeasonID == currentSeason.ID && g.Status!.Name != "Deleted").OrderBy(g => g.Date).ToList(),
                    CurrentUser = SiteUser,
                    CurrentUserPermissions = Permissions,
                    FocusTeam = Team
                };

                ActiveRoster = Team.Invitations
                    .Where(i => i.Player != null
                        && i.Status!.Name == "Active")
                    .OrderBy(i => i.Player!.NameSort)
                    .ToList();

                Substitutes = Team.Invitations
                    .Where(i => i.Player != null
                        && i.Status!.Name == "Substitute")
                    .OrderBy(i => i.Player!.NameSort)
                    .ToList();

                FormerPlayers = Team.Invitations
                    .Where(i => i.Player != null
                        && (i.Status!.Name == "Retired" || i.Status.Name == "Other"))
                    .OrderBy(i => i.Player!.NameSort)
                    .ToList();

                NonPlayerUsers = Team.Invitations
                    .Where(i => i.Player == null
                        && i.Status!.Name != "Hidden")
                    .ToList();

                var managers = Team.Invitations
                    .Where(i => i.InvitationRoles
                        .Any(ir => ir.Role!.Name == "Manager")
                    );
                foreach (var manager in managers) {
                    if (manager.Player != null) {
                        Managers.Add(manager.Player.Name);
                        continue;
                    }
                    if (manager.User != null) {
                        Managers.Add(manager.User.UserLogins.First(ul => ul.IsPrimary).Name);
                        continue;
                    }
                    Managers.Add($"Pending user ({manager.InvitationEmails.First()})");
                }
            }
            else {
                // User confirm team (resends to URL with "search" = team ID)
                SearchResults = "Found " + teamPossibilities.Count().ToString() + " possibilities";
                Teams = teamPossibilities.ToList();
            }
        }

        if (SiteUser != null && Team != null) {
            if (SiteUser.Invitations.Any(i => i.Team == Team)) {
                Permissions.AddRange(SiteUser.Invitations.First(i => i.Team == Team).InvitationRoles.Select(ir => ir.Role!.Name));
                Permissions.Add("Team");
            }
        }

    }

    IEnumerable<Team> FindTeams(string search) {
        var teams = new List<Team>();

        // 1) Search on abbreviation
        teams.AddRange(dbContext.Teams.AsEnumerable().Where(t => t.Abbreviation.Equals(search, StringComparison.OrdinalIgnoreCase)));

        // 2) Search on teamID
        if (Int64.TryParse(search, out long teamID)) {
            teams.AddRange(dbContext.Teams.Where(t => t.ID == teamID));
        }

        return teams.Distinct();
    }

    async Task<Team> GetTeamData(Team team) {
        return await dbContext.Teams
            .Where(t => t == team)
            .Include(t => t.Invitations)
                .ThenInclude(i => i.InvitationEmails)
            .Include(t => t.Invitations)
                .ThenInclude(i => i.InvitationRoles)
                    .ThenInclude(ir => ir.Role)
            .Include(t => t.Invitations)
                .ThenInclude(i => i.User)
                    .ThenInclude(u => u!.UserRoles)
                        .ThenInclude(ur => ur.Role)
            .Include(t => t.Invitations)
                .ThenInclude(i => i.User)
                    .ThenInclude(u => u!.UserLogins)
            .Include(t => t.Invitations)
                .ThenInclude(i => i.Player)
            .Include(t => t.Invitations)
                .ThenInclude(p => p.Status)
            .Include(t => t.GameHostTeam)
                .ThenInclude(g => g.Location)
            .Include(t => t.GameHostTeam)
                .ThenInclude(g => g.Status)
            .Include(t => t.GameVisitingTeam)
                .ThenInclude(g => g.Location)
            .Include(t => t.GameVisitingTeam)
                .ThenInclude(g => g.Status)
            .FirstAsync();
    }
}