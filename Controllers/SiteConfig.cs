using Microsoft.AspNetCore.Authorization;
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

    /// <summary>
    /// Returns the full editable site config (including news settings and history).
    /// </summary>
    [Authorize(Policy = "Scope:Webmaster")]
    [HttpGet("Edit")]
    public async Task<IActionResult> GetEdit()
    {
        var siteConfig = await dbContext.SiteConfigs.FirstOrDefaultAsync();
        if (siteConfig is null)
            return NotFound("Site configuration not found.");

        var home = System.Text.Json.JsonSerializer.Deserialize<SiteHomeConfig>(siteConfig.HomeJson, JsonOptions)
            ?? new SiteHomeConfig();

        var history = System.Text.Json.JsonSerializer.Deserialize<List<SiteHistoryEntry>>(
            siteConfig.HistoryJson, JsonOptions) ?? [];

        // List all files on the volume so the UI can surface orphans
        var filesOnDisk = new List<string>();
        try
        {
            if (Directory.Exists("/var/db/static/files"))
            {
                filesOnDisk = [.. Directory.GetFiles("/var/db/static/files")
                    .Select(Path.GetFileName)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Cast<string>()
                    .OrderBy(n => n)];
            }
        }
        catch
        {
            // Non-fatal: if we can't read the dir, just return an empty list
        }

        return Ok(new
        {
            name = siteConfig.Name,
            shortName = siteConfig.ShortName,
            home,
            history,
            files = filesOnDisk
        });
    }

    /// <summary>
    /// Updates the site configuration.
    /// </summary>
    [Authorize(Policy = "Scope:Webmaster")]
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] SiteConfigUpdateDto dto)
    {
        var siteConfig = await dbContext.SiteConfigs.FirstOrDefaultAsync();
        if (siteConfig is null)
            return NotFound("Site configuration not found.");

        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.ShortName))
            return BadRequest("Name and short name are required.");

        siteConfig.Name = dto.Name.Trim();
        siteConfig.ShortName = dto.ShortName.Trim();
        siteConfig.HomeJson = System.Text.Json.JsonSerializer.Serialize(dto.Home, JsonOptions);
        siteConfig.HistoryJson = System.Text.Json.JsonSerializer.Serialize(dto.History ?? [], JsonOptions);

        dbContext.SiteConfigs.Update(siteConfig);
        await dbContext.SaveChangesAsync();

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Site/Config", "Updated site configuration",
            new { siteConfig.Name, siteConfig.ShortName }));
        await dbContext.SaveChangesAsync();

        return Ok();
    }
}

public record SiteHistoryEntry(int Year, string Result);

public record SiteConfigUpdateDto(
    string Name,
    string ShortName,
    SiteHomeConfig Home,
    List<SiteHistoryEntry>? History);
