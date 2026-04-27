using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/User")]
public class APIUserController(LeagueSitesContext dbContext) : ControllerBase
{
    [HttpGet]
    public IActionResult GetStatus()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new { isAuthenticated = false, name = "", claims = Array.Empty<object>() });
        }

        var name = User.Identity.Name ?? "";
        var claims = User.Claims
            .Where(c => c.Type == "UserID")
            .Select(c => new { type = c.Type, value = c.Value });

        return Ok(new { isAuthenticated = true, name, claims });
    }

    [Authorize]
    [HttpGet("Profile")]
    public async Task<IActionResult> GetProfile()
    {
        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);

        var siteUser = await dbContext.Users
            .AsNoTracking()
            .AsSplitQuery()
            .Include(u => u.Invitations)
                .ThenInclude(i => i.InvitationEmails)
            .Include(u => u.Invitations)
                .ThenInclude(i => i.InvitationRoles)
                    .ThenInclude(r => r.Role)
            .Include(u => u.Invitations)
                .ThenInclude(i => i.Player)
            .Include(u => u.Invitations)
                .ThenInclude(i => i.Status)
            .Include(u => u.Invitations)
                .ThenInclude(i => i.Team)
            .Include(u => u.UserLogins)
                .ThenInclude(l => l.LoginSource)
            .Include(u => u.UserRoles)
                .ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.ID == uid);

        if (siteUser is null)
            return NotFound();

        var hasDeletedNews = await dbContext.News
            .AnyAsync(n => n.AuthorID == siteUser.ID && n.IsDeleted);

        return Ok(new { user = siteUser, hasDeletedNews });
    }

    /// Returns permissions for the current user given a team ID
    [HttpGet("Permissions/{id:long}")]
    public async Task<IActionResult> GetPermissions([FromRoute] long id)
    {
        var permissions = new Dictionary<string, bool>()
        {
            { "Authenticated", false },
            { "Webmaster", false },
            { "Executive", false },
            { "Manager", false },
            { "Scorer", false },
        };

        if (User.Identity is not null && User.Identity.IsAuthenticated)
        {
            permissions["Authenticated"] = true;

            var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
            var user = await dbContext.Users
                .AsNoTracking()
                .Include(u => u.Invitations)
                    .ThenInclude(i => i.InvitationRoles)
                        .ThenInclude(r => r.Role)
                .Include(u => u.UserRoles)
                    .ThenInclude(r => r.Role)
                .FirstOrDefaultAsync(u => u.ID == uid);

            if (user is not null)
            {
                foreach (var ur in user.UserRoles)
                {
                    var roleName = ur.Role?.Name;
                    if (roleName is not null && permissions.ContainsKey(roleName))
                        permissions[roleName] = true;
                }

                var matchingInvitation = user.Invitations.FirstOrDefault(i => i.TeamID == id);
                if (matchingInvitation is not null)
                {
                    foreach (var ir in matchingInvitation.InvitationRoles)
                    {
                        var roleName = ir.Role?.Name;
                        if (roleName is not null && permissions.ContainsKey(roleName))
                            permissions[roleName] = true;
                    }
                }
            }
        }

        return Ok(permissions);
    }

    [HttpPost("Logout")]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
            await HttpContext.SignOutAsync();

            dbContext.Events.Add(Event.Log(
                EventType.Information, uid,
                "/api/User/Logout", "User logged out",
                new { userID = uid, name = User.Identity.Name }));
            await dbContext.SaveChangesAsync();
        }
        return Ok();
    }
}
