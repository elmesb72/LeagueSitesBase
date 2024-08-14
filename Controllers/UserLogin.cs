using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/UserLogin")]
public class APIUserLoginSourceController(LeagueSitesContext context) : ControllerBase
{
    public readonly LeagueSitesContext dbContext = context;

    /// Deletes the provided UserLogin if not primary
    [HttpDelete("Delete/{id}")]
    public async Task<IActionResult> OnDeleteAsync([FromRoute] long id)
    {
        var login = await GetLogin(id);
        if (login is null)
        {
            return StatusCode(400, "Login does not exist. It may have already been deleted.");
        }
        else if (login.IsPrimary)
        {
            return StatusCode(400, "You cannot delete the primary login.");
        }

        if (User.Identity is not null && User.Identity.IsAuthenticated)
        {
            var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
            var user = await dbContext.Users.Include(u => u.UserLogins).FirstOrDefaultAsync(u => u.ID == uid);

            if (user is null)
            {
                return StatusCode(500, $"Cannot find user {id} in database.");
            }
            else if (!user.UserLogins.Any(ul => ul.ID == login.ID))
            {
                return StatusCode(400, "You cannot delete another user's login record, or the login record was not found.");
            }
            else if (user.UserLogins.Count == 1)
            {
                return StatusCode(400, "You cannot delete this login because it's the last login remaining.");
            }
        }
        else
        {
            return StatusCode(401);
        }

        dbContext.UserLogins.Remove(login);
        dbContext.Events.Add(Event.Log(
            EventType.Update,
            Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value),
            "/api/UserLogin/Delete/" + id, "Deleted user login",
            JsonConvert.SerializeObject(login)));
        await dbContext.SaveChangesAsync();

        return StatusCode(200);
    }

    /// Sets the UserLogin as primary
    [HttpPost("Favourite/{id}")]
    public async Task<IActionResult> OnPostUpdateFavouriteAsync([FromRoute] long id)
    {
        var login = await GetLogin(id);
        if (login is null)
        {
            return StatusCode(400, "Login does not exist. Was it deleted?");
        }
        else if ((bool)login.IsPrimary)
        {
            return StatusCode(400, "You cannot set this login as the favourite because it's already the favourite.");
        }

        if (User.Identity is not null && User.Identity.IsAuthenticated)
        {
            var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
            var user = await dbContext.Users.Include(u => u.UserLogins).FirstOrDefaultAsync(u => u.ID == uid);

            if (user is null)
            {
                return StatusCode(500, $"Cannot find user {id} in database.");
            }
            else if (!user.UserLogins.Any(ul => ul.ID == login.ID))
            {
                return StatusCode(400, "You cannot update another user's login record, or the login record was not found.");
            }
            else if (user.UserLogins.Count == 1)
            {
                return StatusCode(400, "You cannot set this login as the favourite because it's already the favourite.");
            }
        }
        else
        {
            return StatusCode(401);
        }

        var logins = await dbContext.UserLogins.Where(ul => ul.UserID == login.UserID).ToListAsync();
        logins.ForEach(ul => ul.IsPrimary = false);
        logins.First(ul => ul.ID == login.ID).IsPrimary = true;
        dbContext.UserLogins.UpdateRange(logins);
        dbContext.Events.Add(Event.Log(EventType.Update, Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value), "/api/UserLogin/Favourite/" + id, "Set login as favourite", JsonConvert.SerializeObject(logins.First(ul => ul.ID == login.ID && (bool)ul.IsPrimary))));
        await dbContext.SaveChangesAsync();

        return StatusCode(200);
    }

    async Task<UserLogin?> GetLogin(long id) => await dbContext.UserLogins.FirstOrDefaultAsync(ul => ul.ID == id);
}