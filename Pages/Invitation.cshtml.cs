using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class InvitationModel(LeagueSitesContext context, IConfiguration config, IWebHostEnvironment env) : PageModel
{
    [BindProperty]
    public InvitationForm? Form { get; set; }
    public Dictionary<string, bool> Permissions { get; set; } = [];

    public List<InvitationStatus> InvitationStatuses { get; set; } = [];

    public List<Team> Teams { get; set; } = [];
    long initialTeamID;

    readonly LeagueSitesContext dbContext = context;
    readonly IConfiguration config = config;
    readonly IWebHostEnvironment env = env;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadPageData();
        if (Permissions.Count == 0)
        {
            return RedirectToPage("/Index/"); // how did you get here?
        }

        if (id != null)
        {
            var invitation = await dbContext.Invitations
                .Include(i => i.Player)
                .Include(i => i.User)
                    .ThenInclude(u => u!.UserRoles)
                        .ThenInclude(ur => ur.Role)
                .Include(i => i.User)
                    .ThenInclude(u => u!.UserLogins)
                .Include(i => i.InvitationEmails)
                .Include(i => i.InvitationRoles)
                    .ThenInclude(ir => ir.Role)
                .FirstOrDefaultAsync(i => i.ID == id);

            if (invitation == default(Invitation))
            {
                return RedirectToPage("/Index"); //URL fudging
            }

            Form = new InvitationForm(invitation);

        }
        else
        {
            Form = new InvitationForm();
            if (initialTeamID != default)
            {
                Form.TeamID = initialTeamID;
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        await LoadPageData();
        if (Permissions.Count == 0)
        {
            return RedirectToPage("/Index/"); // how did you get here?
        }

        if (id != null)
        { // Update existing invitation
            var invitation = await dbContext.Invitations
                .Include(i => i.Player)
                .Include(i => i.Team)
                .Include(i => i.User)
                    .ThenInclude(u => u!.UserRoles)
                        .ThenInclude(ur => ur.Role)
                .Include(i => i.User)
                    .ThenInclude(u => u!.UserLogins)
                .Include(i => i.InvitationEmails)
                .Include(i => i.InvitationRoles)
                    .ThenInclude(ir => ir.Role)
                .FirstOrDefaultAsync(i => i.ID == id);

            if (invitation == default(Invitation))
            {
                return RedirectToPage("/Index"); //URL fudging
            }
            // Update values from InvitationForm
            Form!.UpdateExistingInvitation(ref invitation);

            GeneratePlayerShortCode(ref invitation);

            dbContext.Invitations.Update(invitation);
            dbContext.Events.Add(Event.Log(EventType.Update, Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value), "/Invitation/" + id, "Updated invitation", JsonConvert.SerializeObject(invitation)));
            await dbContext.SaveChangesAsync();

            /*if (!Form.UserExists && !env.IsDevelopment())
            {
                var response = EmailHelper.SendEmail(config["APIKeys:SendGrid"], invitation.InvitationEmails.Select(ie => new EmailAddress(ie.Email)).ToList(), EmailType.Invitation);
            }*/

            return RedirectToPage("/Team", new { search = invitation.Team!.Abbreviation });

        }
        else
        { // Create new invitation

            if (!string.IsNullOrEmpty(Form!.FirstName) && !string.IsNullOrEmpty(Form.LastName))
            {
                Form.PlayerExists = true; // If player information is filled out in a valid manner, ensure player record is created for this invitation.
            }
            var invitation = Form.ToInvitation();

            // If user already exists (via email address), use existing user record
            foreach (var email in invitation.InvitationEmails.Select(ie => ie.Email))
            {
                var userLogin = await dbContext.UserLogins.Include(ul => ul.User).FirstOrDefaultAsync(ul => ul.Email == email);
                if (userLogin != null)
                {
                    invitation.User = userLogin.User;
                    break;
                }
            }

            if (Form.PlayerExists)
            {
                GeneratePlayerShortCode(ref invitation);
                await dbContext.Players.AddAsync(invitation.Player!);
            }
            await dbContext.Invitations.AddAsync(invitation);
            dbContext.Events.Add(Event.Log(EventType.Update, Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value), "/Invitation/", "Added invitation", JsonConvert.SerializeObject(invitation)));
            await dbContext.SaveChangesAsync();

            /*// Email user
            try
            {
                var mailClient = new SendGridClient(config["Email:SendGrid"]);
                var from = new EmailAddress("no-reply@churchleaguefastball.ca", "Church League Fastball");
                var subject = "You've been invited to join the Church League Fastball website!";
                var to = new List<EmailAddress>();
                foreach (var ie in invitation.InvitationEmails)
                {
                    to.Add(new EmailAddress(ie.Email));
                }
                var plainTextContent = "Go to https://churchleaguefastball.ca/Login to join. You must connect using a provider whose account email address matches this one." + Environment.NewLine +
                                        Environment.NewLine +
                                        Environment.NewLine +
                                        "This mailbox is not monitored. Please do not reply.";
                var htmlContent = plainTextContent.Replace(Environment.NewLine, "<br />");
                var message = MailHelper.CreateSingleEmailToMultipleRecipients(from, to, subject, plainTextContent, htmlContent);
                var response = await mailClient.SendEmailAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email on invitation form submission: {ex}");
            }*/

            return RedirectToPage("/Team", new { search = invitation.Team!.Abbreviation });
        }
    }

    async Task LoadPageData()
    {
        InvitationStatuses = await dbContext.InvitationStatuses.ToListAsync();
        Teams = await dbContext.Teams.Where(t => t.Active).ToListAsync();

        Permissions = new Dictionary<string, bool>() {
            { "Webmaster", false },
            { "Executive", false },
            { "Manager", false },
            { "Scorer", false },
            { "Reporter", false },
        };
        if (User.Identity!.IsAuthenticated)
        {
            var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
            var user = await dbContext.Users
                .Include(u => u.Invitations)
                    .ThenInclude(i => i.InvitationRoles)
                        .ThenInclude(r => r.Role)
                .Include(u => u.Invitations)
                    .ThenInclude(i => i.Team)
                .Include(u => u.UserRoles)
                    .ThenInclude(r => r.Role)
                .FirstOrDefaultAsync(u => u.ID == uid);

            if (user != null)
            {
                foreach (var ur in user.UserRoles)
                {
                    Permissions[ur.Role!.Name] = true;
                }

                Permissions["Manager"] = user.Invitations.Any(i => i.InvitationRoles.Any(ir => ir.Role!.Name == "Manager"));
                Permissions["Scorer"] = user.Invitations.Any(i => i.InvitationRoles.Any(ir => ir.Role!.Name == "Scorer"));
                Permissions["Reporter"] = user.Invitations.Any(i => i.InvitationRoles.Any(ir => ir.Role!.Name == "Reporter"));

                foreach (var i in user.Invitations)
                {
                    if (i.InvitationRoles.Any(ir => ir.Role!.Name == "Manager" || ir.Role.Name == "Scorer"))
                    {
                        Permissions[i.Team!.FullName] = true;
                    }
                }

                if (Permissions.Any(p => p.Value) && user.Invitations.Count == 1)
                {
                    initialTeamID = user.Invitations.First().TeamID;
                }
            }
        }

    }

    void GeneratePlayerShortCode(ref Invitation invitation)
    {
        // Generate short code for player
        if (invitation.Player != null && invitation.Player.ID == 0)
        {
            var lastNameCode = invitation.Player.LastName.Length >= 5 ? invitation.Player.LastName[..5] : invitation.Player.LastName;
            var firstNameCode = invitation.Player.FirstName.Length >= 2 ? invitation.Player.FirstName[..2] : invitation.Player.FirstName;
            var nameCode = (lastNameCode + firstNameCode).ToLower();
            var countOfPlayersWithSameName = dbContext.Players.Count(p => p.ShortCode.Length == (nameCode.Length + 2) && p.ShortCode.StartsWith(nameCode));
            invitation.Player.ShortCode = nameCode + (countOfPlayersWithSameName + 1).ToString("00");
        }
    }

}