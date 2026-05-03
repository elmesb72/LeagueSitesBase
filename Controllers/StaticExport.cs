using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/Static")]
[Authorize(Policy = "Scope:Webmaster")]
public class APIStaticExportController(LeagueSitesContext dbContext) : ControllerBase
{
    const string StaticVolume = "/var/db/static";

    /// <summary>
    /// Packages the per-VM static volume (/var/db/static) into a zip
    /// archive and streams it as a download. Mirrors the database
    /// export's pattern: temp file + DeleteOnClose so the response can
    /// stream without keeping the archive in memory, and the temp file
    /// is reclaimed after the client disconnects.
    /// </summary>
    [HttpGet("Export")]
    public async Task<IActionResult> Export()
    {
        if (!Directory.Exists(StaticVolume))
            return NotFound($"Static volume '{StaticVolume}' does not exist on this VM.");

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var tempPath = Path.Combine(Path.GetTempPath(), $"static-export-{timestamp}.zip");

        try
        {
            ZipFile.CreateFromDirectory(StaticVolume, tempPath, CompressionLevel.Optimal,
                includeBaseDirectory: false);

            var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
            dbContext.Events.Add(Event.Log(
                EventType.Update, uid,
                "/api/Static/Export", "Exported static volume",
                new { sizeBytes = new FileInfo(tempPath).Length }));
            await dbContext.SaveChangesAsync();

            // Stream the file; DeleteOnClose reclaims it after the HTTP
            // response completes (or the client disconnects).
            var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.None,
                bufferSize: 81920, options: FileOptions.DeleteOnClose);

            return File(stream, "application/zip", $"LeagueSites-static-{timestamp}.zip");
        }
        catch
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
            throw;
        }
    }
}
