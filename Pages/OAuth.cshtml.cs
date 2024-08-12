using System.Security.Claims;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LeagueSitesBase.Pages;

public class OAuthModel : PageModel
{
    readonly IHttpClientFactory clientFactory;
    readonly IConfiguration configuration;
    readonly LeagueSitesContext dbContext;

    public required string CallbackUrlBase { get; set; }
    public required new User User { get; set; }

    public OAuthModel(IHttpClientFactory clientFactory, IConfiguration configuration, LeagueSitesContext dbContext)
    {
        this.clientFactory = clientFactory;
        this.configuration = configuration;
        this.dbContext = dbContext;
    }

    public async Task<IActionResult> OnGetAsync(string source)
    {

        IAuthProviderLogin? socialLogin = null;

        CallbackUrlBase = "https://" + Request.Host.Value + "/OAuth";

        var client = clientFactory.CreateClient();

        var loginSources = dbContext.UserLoginSources.ToDictionary(x => x.Source, x => x.ID);

        if (source == "Facebook")
        {
            socialLogin = new FacebookLogin(Request.Query, client, configuration, CallbackUrlBase);
        }
        else if (source == "Google")
        {
            socialLogin = new GoogleLogin(Request.Query, client, configuration, CallbackUrlBase);
        }
        else if (source == "Microsoft")
        {
            socialLogin = new MicrosoftLogin(Request.Query, client, configuration, CallbackUrlBase);
        }

        if (socialLogin == null)
        {
            return RedirectToPage("/Index", new { });
        }
        else
        {
            await socialLogin.GetProfileDataAsync();

            if (HttpContext.User.Identity is not null && HttpContext.User.Identity.IsAuthenticated)
            {
                if (socialLogin.WasSuccessful())
                {
                    // If new, and email is not in any invitations other than their own, add user login to existing profile
                    var uid = Convert.ToInt64(HttpContext.User.Claims.First(c => c.Type == "UserID").Value);
                    User = await dbContext.Users
                        .Include(u => u.UserLogins)
                            .ThenInclude(ul => ul.LoginSource)
                        .Where(u => u.ID == uid)
                        .FirstAsync();

                    var loginExists = User.UserLogins.Any(ul => ul.LoginSource!.Source == source && ul.Name == socialLogin.Profile!.Name && ul.Email == socialLogin.Profile.Email);
                    var invitationEmails = await dbContext.InvitationEmails.ToListAsync();
                    var otherInvitationEmails = invitationEmails.Where(ie => !User.UserLogins.Any(ul => ul.Email == ie.Email));
                    if (loginExists)
                    {
                        // Skip ahead
                    }
                    else if (otherInvitationEmails.Any(ie => ie.Email == socialLogin.Profile!.Email))
                    {
                        // Redirect user to User page with error message
                        return RedirectToPage("/User?error=Email address already exists on another invitation.");
                    }
                    else
                    {
                        var ul = new UserLogin()
                        {
                            UserID = User.ID,
                            LoginSourceID = loginSources[source],
                            Name = socialLogin.Profile!.Name,
                            Email = socialLogin.Profile!.Email,
                            IsPrimary = false
                        };
                        await dbContext.UserLogins.AddAsync(ul);
                        dbContext.Events.Add(Event.Log(EventType.Update, uid, "/OAuth/" + source, "Associating " + source + " UserLogin", JsonConvert.SerializeObject(ul)));
                        await dbContext.SaveChangesAsync();
                    }

                    return RedirectToPage("/User");
                }
                else
                {
                    // Redirect user to User page with error message
                    return RedirectToPage("/User?error=" + socialLogin.Errors?.First().ToString());
                }
            }
            else
            {
                if (socialLogin.WasSuccessful())
                {
                    // Map profile to this Login
                    User = new User();
                    var login = new UserLogin()
                    {
                        LoginSourceID = loginSources[source],
                        Name = socialLogin.Profile!.Name,
                        Email = socialLogin.Profile!.Email,
                        IsPrimary = true,
                    };
                    User.UserLogins.Add(login);

                    // Check for existing login
                    var existingLogins = await dbContext.UserLogins
                        .Include(ul => ul.User)
                        .Include(ul => ul.LoginSource)
                        .Where(ul => ul.Email == login.Email)
                        .ToListAsync();

                    if (existingLogins.Any(ul => ul.LoginSource!.Source == source))
                    { // User exists via this auth provider and email address
                        return await SignIn(existingLogins.First().User!);
                    }
                    else if (existingLogins.Count > 0)
                    { // User exists via another auth provider with this email address
                      // Associate this login with existing user, and add to database
                        login.UserID = existingLogins.First().UserID;
                        login.IsPrimary = false;
                        await dbContext.UserLogins.AddAsync(login);
                        dbContext.Events.Add(Event.Log(EventType.Update, login.UserID, "/OAuth/" + source, "Associating " + source + " UserLogin", JsonConvert.SerializeObject(login)));
                        await dbContext.SaveChangesAsync();

                        return await SignIn(existingLogins.First().User!);
                    }
                    else
                    { // User doesn't exist under this email address
                        var invitations = await dbContext.Invitations
                            .Include(i => i.InvitationEmails)
                            .Where(i => i.InvitationEmails.Any(e => e.Email == login.Email))
                            .ToListAsync();

                        if (invitations.Count > 0)
                        {
                            // Add new user to database, which will populate the User's ID value upon addition
                            await dbContext.Users.AddAsync(User);
                            await dbContext.SaveChangesAsync();

                            // Associate user with invitation
                            invitations.ForEach(i => i.UserID = User.ID);
                            dbContext.Invitations.UpdateRange(invitations);
                            await dbContext.SaveChangesAsync();

                            // Remove other email addresses from these invitations; the user will manage them on their own after creation
                            var emailsToRemove = invitations.SelectMany(i => i.InvitationEmails).Where(e => e.Email != login.Email);
                            dbContext.InvitationEmails.RemoveRange(emailsToRemove);
                            await dbContext.SaveChangesAsync();

                            // Log result
                            dbContext.Events.Add(Event.Log(EventType.Update, User.ID, "/OAuth/" + source, "Registered new user", JsonConvert.SerializeObject(invitations)));
                            await dbContext.SaveChangesAsync();

                            return await SignIn(User);
                        }
                        else
                        {
                            // Reject user for no invitation
                            return RedirectToPage("/Login", new { error = "invitation", error_details = HttpUtility.UrlEncode(User.UserLogins.First().Email) });
                        }
                    }
                }
                else
                {
                    // Redirect user to Login page with error message
                    var error = socialLogin.Errors?.First();
                    return RedirectToPage("/Login", new { error = error?.Key, error_details = HttpUtility.UrlEncode(error?.Value) });
                }
            }

        }

    }

    async Task<IActionResult> SignIn(User user)
    {
        // Create the identity from existing login
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.Name, user.UserLogins.First(x => x.IsPrimary).Name));
        identity.AddClaim(new Claim("UserID", user.ID.ToString()));

        // Authenticate using the identity
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = true });

        return RedirectToPage("/User");
    }

}