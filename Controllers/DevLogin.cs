using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/DevLogin")]
public class APIDevLoginController(LeagueSitesContext dbContext, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Login()
    {
        if (!env.IsDevelopment())
            return NotFound();

        // Find the first user with a Webmaster role
        var user = await dbContext.Users
            .Include(u => u.UserLogins)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserRoles.Any(ur => ur.Role!.Name == "Webmaster"));

        if (user is null)
            return BadRequest("No webmaster user found in the database.");

        var primaryLogin = user.UserLogins.FirstOrDefault(ul => ul.IsPrimary)
            ?? user.UserLogins.FirstOrDefault();

        if (primaryLogin is null)
            return BadRequest("Webmaster user has no login records.");

        var identity = new ClaimsIdentity(
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.Name, primaryLogin.Name));
        identity.AddClaim(new Claim("UserID", user.ID.ToString()));

        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        return Ok(new { name = primaryLogin.Name, userId = user.ID });
    }
}
