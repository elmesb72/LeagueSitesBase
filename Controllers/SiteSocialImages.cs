using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

[ApiController]
[Route("api/Site/SocialImages")]
[Authorize(Policy = "Scope:Webmaster")]
public class APISiteSocialImagesController(LeagueSitesContext dbContext) : ControllerBase
{
    // Social icons live at /var/db/static/images/social/ and are served
    // publicly at /images/social/ via an Apache alias. Each file is named
    // {platformKey}.webp to match the homepage's existing <img src> pattern.
    const string SocialImagesDirectory = "/var/db/static/images/social";
    const int MaxDimension = 128;
    const int WebpQuality = 90;

    // Platform keys must be safe to use as a filename and in URLs with no
    // encoding. This matches the assumption on the homepage, which builds
    // <img src="/images/social/{platform}.webp"> directly.
    static readonly Regex ValidPlatformKey = new(@"^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    /// <summary>
    /// Uploads (and converts to WebP) a social icon for the given platform
    /// key. The platform key must match the key used in SiteConfig.socials
    /// exactly, since both feed the same /images/social/{key}.webp URL.
    /// </summary>
    [HttpPost("{platformKey}")]
    [RequestSizeLimit(2 * 1024 * 1024)] // 2 MB
    public async Task<IActionResult> Upload([FromRoute] string platformKey, IFormFile file)
    {
        if (!ValidPlatformKey.IsMatch(platformKey))
            return BadRequest("Platform key may only contain letters, numbers, hyphens, and underscores.");

        if (file is null || file.Length == 0)
            return BadRequest("No file uploaded.");

        // ImageSharp doesn't rasterize SVG; reject it explicitly so the
        // caller gets a clear error instead of a generic decode failure.
        if (LooksLikeSvg(file))
            return BadRequest("SVG uploads are not supported. Please upload a raster image (PNG, JPG, or WebP).");

        Directory.CreateDirectory(SocialImagesDirectory);
        var path = Path.Combine(SocialImagesDirectory, $"{platformKey}.webp");

        try
        {
            using var source = file.OpenReadStream();
            using var image = await Image.LoadAsync(source);

            // Downscale to fit within MaxDimension x MaxDimension while
            // preserving aspect ratio. No upscaling — Mode.Max keeps smaller
            // images at their original size.
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(MaxDimension, MaxDimension),
                Mode = ResizeMode.Max
            }));

            await using var output = new FileStream(path, FileMode.Create, FileAccess.Write);
            await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = WebpQuality });
        }
        catch (UnknownImageFormatException)
        {
            return BadRequest("Unsupported or unrecognized image format.");
        }
        catch (InvalidImageContentException)
        {
            return BadRequest("Image file is corrupted or could not be decoded.");
        }

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            $"/api/Site/SocialImages/{platformKey}", "Uploaded social icon",
            new { platformKey, originalFilename = file.FileName, sizeBytes = file.Length }));
        await dbContext.SaveChangesAsync();

        return Ok(new { platformKey, path = $"/images/social/{platformKey}.webp" });
    }

    /// <summary>
    /// Deletes the social icon for the given platform key.
    /// </summary>
    [HttpDelete("{platformKey}")]
    public async Task<IActionResult> Delete([FromRoute] string platformKey)
    {
        if (!ValidPlatformKey.IsMatch(platformKey))
            return BadRequest("Platform key may only contain letters, numbers, hyphens, and underscores.");

        var path = Path.Combine(SocialImagesDirectory, $"{platformKey}.webp");
        if (!System.IO.File.Exists(path))
            return NotFound();

        System.IO.File.Delete(path);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            $"/api/Site/SocialImages/{platformKey}", "Deleted social icon",
            new { platformKey }));
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Heuristic SVG check: XML/SVG content is text-based and ImageSharp
    /// will throw a generic decode error if we try to load it. Catching it
    /// early gives a clearer message to the user.
    /// </summary>
    static bool LooksLikeSvg(IFormFile file)
    {
        var type = file.ContentType ?? string.Empty;
        if (type.Contains("svg", StringComparison.OrdinalIgnoreCase)) return true;

        var name = file.FileName ?? string.Empty;
        return name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    }
}
