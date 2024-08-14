using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LeagueSitesBase.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel(ILogger<ErrorModel> logger, LeagueSitesContext context, IConfiguration config, IWebHostEnvironment environment) : PageModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    readonly ILogger<ErrorModel> _logger = logger;
    readonly LeagueSitesContext _context = context;
    readonly IConfiguration _config = config;
    readonly IWebHostEnvironment _environment = environment;

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        long uid = -1;
        var identity = HttpContext.User.Identity;
        if (identity is not null && identity.IsAuthenticated)
        {
            uid = Convert.ToInt64(HttpContext.User.Claims.First(c => c.Type == "UserID").Value);
        }

        var exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var source = exceptionHandlerPathFeature?.Path ?? "Unknown error path";
        var error = exceptionHandlerPathFeature?.Error ?? new Exception("Unknown error");

        try
        {
            // Add exception to DB
            _context.Events.Add(Event.Log(EventType.Error, uid, source, error.GetType().Name, error.ToString()));
            _context.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError("An error occurred when trying to write another error adding to database: {ex}", ex);
        }
    }
}
