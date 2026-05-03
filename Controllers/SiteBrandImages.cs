using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Upload/delete for site-wide brand images: the league logo
/// (/images/logo.webp) and the favicon (/favicon.png). Thin wrappers
/// around ImageProcessor with per-image output format and size settings.
/// </summary>
[ApiController]
[Route("api/Site")]
[Authorize(Policy = "Scope:Webmaster")]
public class APISiteBrandImagesController(LeagueSitesContext dbContext) : ControllerBase
{
    const string LogoPath = "/var/db/static/images/logo.webp";
    const int LogoMaxDimension = 256;

    // Favicon lives at the root of the static volume (not under images/)
    // because browsers request /favicon.png directly. PNG output keeps
    // favicon support universal — WebP favicon support is spotty.
    const string FaviconPath = "/var/db/static/favicon.png";
    const int FaviconMaxDimension = 64;

    [HttpPost("Logo")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        var result = await ImageProcessor.ConvertAsync(
            file, LogoPath, ImageOutputFormat.Webp, LogoMaxDimension);
        if (!result.Success)
            return BadRequest(result.ErrorMessage);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Site/Logo", "Uploaded league logo",
            new { originalFilename = file.FileName, sizeBytes = file.Length }));
        await dbContext.SaveChangesAsync();

        return Ok(new { path = "/images/logo.webp" });
    }

    [HttpDelete("Logo")]
    public async Task<IActionResult> DeleteLogo()
    {
        if (!System.IO.File.Exists(LogoPath))
            return NotFound();

        System.IO.File.Delete(LogoPath);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Site/Logo", "Deleted league logo", new { }));
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("Favicon")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadFavicon(IFormFile file)
    {
        var result = await ImageProcessor.ConvertAsync(
            file, FaviconPath, ImageOutputFormat.Png, FaviconMaxDimension);
        if (!result.Success)
            return BadRequest(result.ErrorMessage);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Site/Favicon", "Uploaded favicon",
            new { originalFilename = file.FileName, sizeBytes = file.Length }));
        await dbContext.SaveChangesAsync();

        return Ok(new { path = "/favicon.png" });
    }

    [HttpDelete("Favicon")]
    public async Task<IActionResult> DeleteFavicon()
    {
        if (!System.IO.File.Exists(FaviconPath))
            return NotFound();

        System.IO.File.Delete(FaviconPath);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Site/Favicon", "Deleted favicon", new { }));
        await dbContext.SaveChangesAsync();

        return NoContent();
    }
}
