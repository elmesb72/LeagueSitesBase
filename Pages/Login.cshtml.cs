using System;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LeagueSitesBase.Pages;

public class LoginModel : PageModel
{
    public String CallbackUrlBase { get; set; }
    public String Error { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity.IsAuthenticated)
        {
            return RedirectToPage("/User", new { });
        }

        CallbackUrlBase = "https://" + Request.Host.Value + "/OAuth";

        if (Request.Query.ContainsKey("error"))
        {
            String error = Request.Query["error"];
            String errorDetails = null;
            if (Request.Query.ContainsKey("error_details"))
            {
                errorDetails = HttpUtility.UrlDecode(Request.Query["error_details"]);
            }
            Error = error switch
            {
                "invitation" => $"The email address \"{errorDetails ?? "(not provided)"}\" is not associated with any valid invitations. Please contact your manager.",
                "token" => $"There was an error getting your profile data from the provider: {errorDetails ?? "No error info was provided."}",
                "code" => $"There was an error on the provider's end: {errorDetails ?? "No error info was provided."}",
                "user" => "Login failed: You did not allow access to the site.",
                _ => "Unknown error. Are you messing around with the URL?",
            };
        }

        return Page();
    }
}