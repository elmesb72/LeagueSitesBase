using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/Login")]
public class APILoginController(IConfiguration config, IWebHostEnvironment env) : ControllerBase
{
    static readonly Dictionary<string, Func<string, string, string>> ProviderUrlGenerators = new()
    {
        ["Google"] = (clientId, callbackUrl) =>
            $"https://accounts.google.com/o/oauth2/v2/auth?scope=profile email&client_id={clientId}&redirect_uri={callbackUrl}/Google&response_type=code",

        ["Microsoft"] = (clientId, callbackUrl) =>
            $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id={clientId}&response_type=code&redirect_uri={callbackUrl}/Microsoft&scope=User.Read&prompt=consent",

        ["Facebook"] = (clientId, callbackUrl) =>
            $"https://www.facebook.com/v6.0/dialog/oauth?scope=email&client_id={clientId}&redirect_uri={callbackUrl}/Facebook",
    };

    public static IReadOnlyCollection<string> SupportedProviders => ProviderUrlGenerators.Keys;

    [ResponseCache(Duration = 300)]
    [HttpGet]
    public IActionResult Get()
    {
        var callbackUrlBase = $"https://{Request.Host.Value}/OAuth";
        var authMethods = config.GetSection("Authentication").GetChildren();

        var providers = authMethods
            .Where(m => ProviderUrlGenerators.ContainsKey(m.Key))
            .Select(m => new
            {
                name = m.Key,
                url = ProviderUrlGenerators[m.Key](
                    config[$"Authentication:{m.Key}:ClientID"] ?? "",
                    callbackUrlBase)
            })
            .ToList();

        return Ok(new
        {
            providers,
            isDevelopment = env.IsDevelopment()
        });
    }
}
