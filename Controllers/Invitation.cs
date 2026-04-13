using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Invitation")]
[Authorize(Policy = "Scope:Manager,Scorer,Reporter,Executive,Webmaster")]
public class APIInvitationController(LeagueSitesContext dbContext) : ControllerBase
{
    [HttpGet("Create")]
    public async Task<IActionResult> GetCreateData()
    {
        var teams = await dbContext.Teams
            .AsNoTracking()
            .Where(t => t.Active)
            .OrderBy(t => t.Name)
            .Select(t => new { t.ID, t.FullName, t.Abbreviation, t.BackgroundColor, t.Color })
            .ToListAsync();

        var statuses = await dbContext.InvitationStatuses
            .AsNoTracking()
            .Select(s => new { s.ID, s.Name })
            .ToListAsync();

        return Ok(new { teams, statuses });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get([FromRoute] long id)
    {
        var invitation = await dbContext.Invitations
            .AsNoTracking()
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

        if (invitation is null)
            return NotFound();

        return Ok(invitation);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] InvitationForm form)
    {
        if (!string.IsNullOrEmpty(form.FirstName) && !string.IsNullOrEmpty(form.LastName))
            form.PlayerExists = true;

        var invitation = form.ToInvitation();

        // If user already exists via email, associate them
        foreach (var email in invitation.InvitationEmails.Select(ie => ie.Email))
        {
            var userLogin = await dbContext.UserLogins
                .Include(ul => ul.User)
                .FirstOrDefaultAsync(ul => ul.Email == email);
            if (userLogin != null)
            {
                invitation.User = userLogin.User;
                break;
            }
        }

        if (form.PlayerExists)
        {
            GeneratePlayerShortCode(invitation);
            await dbContext.Players.AddAsync(invitation.Player!);
        }

        await dbContext.Invitations.AddAsync(invitation);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Invitation", "Added invitation",
            JsonConvert.SerializeObject(invitation)));

        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = invitation.ID }, invitation);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update([FromRoute] long id, [FromBody] InvitationForm form)
    {
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

        if (invitation is null)
            return NotFound();

        form.UpdateExistingInvitation(ref invitation);
        GeneratePlayerShortCode(invitation);

        dbContext.Invitations.Update(invitation);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Invitation/" + id, "Updated invitation",
            JsonConvert.SerializeObject(invitation)));

        await dbContext.SaveChangesAsync();

        return Ok(invitation);
    }

    void GeneratePlayerShortCode(Invitation invitation)
    {
        if (invitation.Player != null && invitation.Player.ID == 0)
        {
            var lastNameCode = invitation.Player.LastName.Length >= 5
                ? invitation.Player.LastName[..5]
                : invitation.Player.LastName;
            var firstNameCode = invitation.Player.FirstName.Length >= 2
                ? invitation.Player.FirstName[..2]
                : invitation.Player.FirstName;
            var nameCode = (lastNameCode + firstNameCode).ToLower();
            var count = dbContext.Players
                .Count(p => p.ShortCode.Length == (nameCode.Length + 2) && p.ShortCode.StartsWith(nameCode));
            invitation.Player.ShortCode = nameCode + (count + 1).ToString("00");
        }
    }
}
