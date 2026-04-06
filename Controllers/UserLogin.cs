using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/UserLogin")]
public class APIUserLoginController(LeagueSitesContext context) : ControllerBase
{
    readonly LeagueSitesContext dbContext = context;

    /// Deletes the provided UserLogin if not primary
    [Authorize]
    [HttpDelete("Delete/{id:long}")]
    public async Task<IActionResult> Delete([FromRoute] long id)
    {
        var login = await GetLogin(id);
        if (login is null)
            return BadRequest("Login does not exist. It may have already been deleted.");
        if (login.IsPrimary)
            return BadRequest("You cannot delete the primary login.");

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        var user = await dbContext.Users.Include(u => u.UserLogins).FirstOrDefaultAsync(u => u.ID == uid);

        if (user is null)
            return NotFound($"Cannot find user {uid} in database.");
        if (!user.UserLogins.Any(ul => ul.ID == login.ID))
            return BadRequest("You cannot delete another user's login record, or the login record was not found.");
        if (user.UserLogins.Count == 1)
            return BadRequest("You cannot delete this login because it's the last login remaining.");

        dbContext.UserLogins.Remove(login);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/UserLogin/Delete/" + id, "Deleted user login",
            JsonConvert.SerializeObject(login)));
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// Sets the UserLogin as primary
    [Authorize]
    [HttpPost("Favourite/{id:long}")]
    public async Task<IActionResult> SetFavourite([FromRoute] long id)
    {
        var login = await GetLogin(id);
        if (login is null)
            return BadRequest("Login does not exist. Was it deleted?");
        if (login.IsPrimary)
            return BadRequest("This login is already the favourite.");

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        var user = await dbContext.Users.Include(u => u.UserLogins).FirstOrDefaultAsync(u => u.ID == uid);

        if (user is null)
            return NotFound($"Cannot find user {uid} in database.");
        if (!user.UserLogins.Any(ul => ul.ID == login.ID))
            return BadRequest("You cannot update another user's login record, or the login record was not found.");
        if (user.UserLogins.Count == 1)
            return BadRequest("This login is already the favourite.");

        var logins = await dbContext.UserLogins.Where(ul => ul.UserID == login.UserID).ToListAsync();
        logins.ForEach(ul => ul.IsPrimary = false);
        logins.First(ul => ul.ID == login.ID).IsPrimary = true;
        dbContext.UserLogins.UpdateRange(logins);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/UserLogin/Favourite/" + id, "Set login as favourite",
            JsonConvert.SerializeObject(logins.First(ul => ul.ID == login.ID))));
        await dbContext.SaveChangesAsync();

        return Ok();
    }

    async Task<UserLogin?> GetLogin(long id) =>
        await dbContext.UserLogins.FirstOrDefaultAsync(ul => ul.ID == id);
}
