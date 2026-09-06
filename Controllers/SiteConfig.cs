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
                // aboutBlurb is authored as Markdown but consumed by the
                // homepage via {@html}, so render it here.
                aboutBlurb = MarkdownHelper.ToHtml(home.AboutBlurb),
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

        // List uploaded social icon platform keys (filename without the
        // .webp extension). Used by the Webmaster UI to preview existing
        // icons alongside each Social Links row.
        var socialImagesOnDisk = new List<string>();
        try
        {
            if (Directory.Exists("/var/db/static/images/social"))
            {
                socialImagesOnDisk = [.. Directory.GetFiles("/var/db/static/images/social", "*.webp")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Cast<string>()
                    .OrderBy(n => n)];
            }
        }
        catch
        {
            // Non-fatal
        }

        return Ok(new
        {
            name = siteConfig.Name,
            shortName = siteConfig.ShortName,
            home,
            history,
            // Current standings rules with defaults filled in, plus the full
            // comparator registry so the UI never hardcodes the choices.
            standings = StandingsConfigService.Parse(siteConfig.StandingsJson),
            standingsComparators = StandingsComparators.All
                .Select(c => new { c.Name, c.Description, c.GroupRestricted }),
            files = filesOnDisk,
            socialImages = socialImagesOnDisk,
            hasLogo = System.IO.File.Exists("/var/db/static/images/logo.webp"),
            hasFavicon = System.IO.File.Exists("/var/db/static/favicon.png")
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

        // Standings rules are optional in the payload (older clients); when
        // present they must be fully valid before anything is persisted.
        if (dto.Standings is not null)
        {
            var problems = StandingsConfigService.Validate(dto.Standings);
            if (problems.Count > 0)
                return BadRequest(string.Join(" ", problems));
        }

        siteConfig.Name = dto.Name.Trim();
        siteConfig.ShortName = dto.ShortName.Trim();
        siteConfig.HomeJson = System.Text.Json.JsonSerializer.Serialize(dto.Home, JsonOptions);
        siteConfig.HistoryJson = System.Text.Json.JsonSerializer.Serialize(dto.History ?? [], JsonOptions);
        if (dto.Standings is not null)
            siteConfig.StandingsJson = System.Text.Json.JsonSerializer.Serialize(dto.Standings, JsonOptions);

        dbContext.SiteConfigs.Update(siteConfig);
        await dbContext.SaveChangesAsync();

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Site/Config", "Updated site configuration",
            new { siteConfig.Name, siteConfig.ShortName, Standings = dto.Standings }));
        await dbContext.SaveChangesAsync();

        return Ok();
    }
}

public record SiteHistoryEntry(int Year, string Result);

public record SiteConfigUpdateDto(
    string Name,
    string ShortName,
    SiteHomeConfig Home,
    List<SiteHistoryEntry>? History,
    StandingsConfig? Standings = null);
