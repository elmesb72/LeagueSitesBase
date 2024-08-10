using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/User")]
public class APIUserPermissionsController : ControllerBase
{
    //public List<Player> Players { get; set; }

    public readonly LeagueSitesContext dbContext;
    public APIUserPermissionsController(LeagueSitesContext context) => dbContext = context;

    /// Returns a list of permissions for the current user given a teamID
    [HttpGet("Permissions/{id}")]
    public async Task<IActionResult> OnGetAsync([FromRoute] long id)
    {
        var permissions = new Dictionary<string, bool>()
            {
                { "Webmaster", false },
                { "Executive", false },
                { "Manager", false },
                { "Scorer", false },
            };
        if (User.Identity is not null && User.Identity.IsAuthenticated)
        {
            var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
            var user = await dbContext.Users
                .Include(u => u.Invitations)
                    .ThenInclude(i => i.InvitationRoles)
                        .ThenInclude(r => r.Role)
                .Include(u => u.Invitations)
                .Include(u => u.UserRoles)
                    .ThenInclude(r => r.Role)
                .FirstOrDefaultAsync(u => u.ID == uid);

            if (user is not null)
            {
                foreach (UserRole ur in user.UserRoles)
                {
                    var roleName = ur.Role?.Name;
                    if (roleName is null)
                    {
                        continue;
                    }
                    permissions[roleName] = true;
                }
                var matchingInvitation = user.Invitations.FirstOrDefault(i => i.TeamID == id);
                if (matchingInvitation != default(Invitation))
                {
                    foreach (InvitationRole ir in matchingInvitation.InvitationRoles)
                    {
                        var roleName = ir.Role?.Name;
                        if (roleName is null)
                        {
                            continue;
                        }
                        permissions[roleName] = true;
                    }
                }
            }


        }

        return new JsonResult(permissions);
    }

}