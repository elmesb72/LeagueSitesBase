using System.Security.Claims;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/OAuth")]
public class APIOAuthController(IHttpClientFactory clientFactory, IConfiguration configuration, LeagueSitesContext dbContext) : ControllerBase
{
    [HttpGet("{source}")]
    public async Task<IActionResult> Callback(string source)
    {
        var callbackUrlBase = $"https://{Request.Host.Value}/api/OAuth";
        var client = clientFactory.CreateClient();
        var loginSources = dbContext.UserLoginSources.ToDictionary(x => x.Source, x => x.ID);

        IAuthProviderLogin? socialLogin = source switch
        {
            "Facebook" => new FacebookLogin(Request.Query, client, configuration, callbackUrlBase),
            "Google" => new GoogleLogin(Request.Query, client, configuration, callbackUrlBase),
            "Microsoft" => new MicrosoftLogin(Request.Query, client, configuration, callbackUrlBase),
            _ => null
        };

        if (socialLogin == null)
            return Redirect("/");

        await socialLogin.GetProfileDataAsync();

        if (HttpContext.User.Identity is not null && HttpContext.User.Identity.IsAuthenticated)
            return await HandleAuthenticatedUser(source, socialLogin, loginSources);
        else
            return await HandleUnauthenticatedUser(source, socialLogin, loginSources);
    }

    async Task<IActionResult> HandleAuthenticatedUser(string source, IAuthProviderLogin socialLogin, Dictionary<string, long> loginSources)
    {
        if (socialLogin.WasSuccessful())
        {
            var uid = Convert.ToInt64(HttpContext.User.Claims.First(c => c.Type == "UserID").Value);
            var user = await dbContext.Users
                .Include(u => u.UserLogins)
                    .ThenInclude(ul => ul.LoginSource)
                .Where(u => u.ID == uid)
                .FirstAsync();

            var loginExists = user.UserLogins.Any(ul =>
                ul.LoginSource!.Source == source &&
                ul.Name == socialLogin.Profile!.Name &&
                ul.Email == socialLogin.Profile.Email);

            if (!loginExists)
            {
                var invitationEmails = await dbContext.InvitationEmails.ToListAsync();
                var otherInvitationEmails = invitationEmails.Where(ie => !user.UserLogins.Any(ul => ul.Email == ie.Email));

                if (otherInvitationEmails.Any(ie => ie.Email == socialLogin.Profile!.Email))
                    return Redirect("/User?error=Email address already exists on another invitation.");

                var ul = new UserLogin()
                {
                    UserID = user.ID,
                    LoginSourceID = loginSources[source],
                    Name = socialLogin.Profile!.Name,
                    Email = socialLogin.Profile!.Email,
                    IsPrimary = false
                };
                await dbContext.UserLogins.AddAsync(ul);
                dbContext.Events.Add(Event.Log(EventType.Update, uid, "/api/OAuth/" + source, "Associating " + source + " UserLogin",
                    new { ul.UserID, ul.LoginSourceID, ul.Name, ul.Email, ul.IsPrimary }));
                await dbContext.SaveChangesAsync();
            }

            return Redirect("/User");
        }
        else
        {
            return Redirect("/User?error=" + socialLogin.Errors?.First().ToString());
        }
    }

    async Task<IActionResult> HandleUnauthenticatedUser(string source, IAuthProviderLogin socialLogin, Dictionary<string, long> loginSources)
    {
        if (!socialLogin.WasSuccessful())
        {
            var error = socialLogin.Errors?.First();
            return Redirect($"/Login?error={error?.Key}&error_details={HttpUtility.UrlEncode(error?.Value)}");
        }

        var login = new UserLogin()
        {
            LoginSourceID = loginSources[source],
            Name = socialLogin.Profile!.Name,
            Email = socialLogin.Profile!.Email,
            IsPrimary = true,
        };

        var existingLogins = await dbContext.UserLogins
            .Include(ul => ul.User)
            .Include(ul => ul.LoginSource)
            .Where(ul => ul.Email == login.Email)
            .ToListAsync();

        if (existingLogins.Any(ul => ul.LoginSource!.Source == source))
        {
            return await SignIn(existingLogins.First().User!);
        }
        else if (existingLogins.Count > 0)
        {
            login.UserID = existingLogins.First().UserID;
            login.IsPrimary = false;
            await dbContext.UserLogins.AddAsync(login);
            dbContext.Events.Add(Event.Log(EventType.Update, login.UserID, "/api/OAuth/" + source, "Associating " + source + " UserLogin",
                new { login.UserID, login.LoginSourceID, login.Name, login.Email, login.IsPrimary }));
            await dbContext.SaveChangesAsync();
            return await SignIn(existingLogins.First().User!);
        }
        else
        {
            var invitations = await dbContext.Invitations
                .Include(i => i.InvitationEmails)
                .Where(i => i.InvitationEmails.Any(e => e.Email == login.Email))
                .ToListAsync();

            if (invitations.Count > 0)
            {
                var user = new User();
                user.UserLogins.Add(login);
                await dbContext.Users.AddAsync(user);
                await dbContext.SaveChangesAsync();

                invitations.ForEach(i => i.UserID = user.ID);
                dbContext.Invitations.UpdateRange(invitations);
                await dbContext.SaveChangesAsync();

                var emailsToRemove = invitations.SelectMany(i => i.InvitationEmails).Where(e => e.Email != login.Email);
                dbContext.InvitationEmails.RemoveRange(emailsToRemove);
                await dbContext.SaveChangesAsync();

                dbContext.Events.Add(Event.Log(EventType.Update, user.ID, "/api/OAuth/" + source, "Registered new user",
                    invitations.Select(i => new { i.ID, i.TeamID, i.UserID, i.PlayerID, i.StatusID })));
                await dbContext.SaveChangesAsync();

                return await SignIn(user);
            }
            else
            {
                return Redirect($"/Login?error=invitation&error_details={HttpUtility.UrlEncode(login.Email)}");
            }
        }
    }

    async Task<IActionResult> SignIn(User user)
    {
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.Name, user.UserLogins.First(x => x.IsPrimary).Name));
        identity.AddClaim(new Claim("UserID", user.ID.ToString()));

        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = true });

        return Redirect("/User");
    }
}
