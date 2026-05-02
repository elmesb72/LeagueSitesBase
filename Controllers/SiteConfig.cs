using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Site/Config")]
public class APISiteConfigController(LeagueSitesContext dbContext, IConfiguration config) : ControllerBase
{
    static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    [ResponseCache(Duration = 300)]
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var siteConfig = await dbContext.SiteConfigs.FirstOrDefaultAsync();
        if (siteConfig is null)
            return NotFound("Site configuration not found.");

        var home = System.Text.Json.JsonSerializer.Deserialize<SiteHomeConfig>(siteConfig.HomeJson, JsonOptions)
            ?? new SiteHomeConfig();

        // Filter out empty socials (same behavior as the old IConfiguration-based code)
        var socials = home.Socials
            .Where(s => !string.IsNullOrEmpty(s.Value))
            .ToDictionary(s => s.Key, s => s.Value);

        return Ok(new
        {
            siteName = siteConfig.Name,
            shortName = siteConfig.ShortName,
            home = new
            {
                aboutBlurb = home.AboutBlurb,
                executives = home.Executives,
                socials,
                links = home.Links,
                information = home.Information
            },
            apiKeys = new
            {
                googleMaps = config["APIKeys:GoogleMaps"]
            }
        });
    }
}
