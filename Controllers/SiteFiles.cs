using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Site/Files")]
[Authorize(Policy = "Scope:Webmaster")]
public class APISiteFilesController(LeagueSitesContext dbContext) : ControllerBase
{
    // Files live at /var/db/static/files/ and are served publicly via the
    // /files/ Apache Alias. Only webmasters can upload or delete.
    const string FilesDirectory = "/var/db/static/files";

    // Matches any character that's not alphanumeric, dot, hyphen, or underscore.
    static readonly Regex UnsafeFilenameChars = new(@"[^A-Za-z0-9._-]", RegexOptions.Compiled);

    // Extensions we allow webmasters to upload via Information Links.
    // Intentionally conservative: common document, spreadsheet, image, and
    // plain-text formats only. Archives, executables, and scripts are all
    // rejected so a stray file dropped in the volume can't be turned into
    // a drive-by install or a cross-site scripting vector.
    //
    // SVG is blocked alongside scripts because SVG can embed JavaScript
    // that runs when the file is opened or rendered inline.
    //
    // TODO: extension-only validation is cheap but fools nobody — a binary
    // renamed to .pdf still passes. When we care about hardening this
    // further, add a magic-byte check for the common document types.
    static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx", ".xlsx", ".pptx",
        ".doc", ".xls", ".ppt",
        ".txt", ".csv", ".md",
        ".png", ".jpg", ".jpeg", ".webp", ".gif"
    };

    /// <summary>
    /// Uploads a file to the static files directory. Overwrites any existing
    /// file with the same (sanitized) name.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var sanitized = SanitizeFilename(file.FileName);
        if (string.IsNullOrEmpty(sanitized))
            return BadRequest("Filename is invalid or empty after sanitization.");

        var extension = Path.GetExtension(sanitized);
        if (!AllowedExtensions.Contains(extension))
        {
            var allowed = string.Join(", ", AllowedExtensions.OrderBy(e => e));
            return BadRequest($"File type '{extension}' is not allowed. Allowed types: {allowed}.");
        }

        Directory.CreateDirectory(FilesDirectory);
        var path = Path.Combine(FilesDirectory, sanitized);

        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            await file.CopyToAsync(stream);
        }

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Site/Files", "Uploaded file",
            new { filename = sanitized, sizeBytes = file.Length }));
        await dbContext.SaveChangesAsync();

        return Ok(new { filename = sanitized, path = $"/files/{sanitized}" });
    }

    /// <summary>
    /// Deletes a file from the static files directory.
    /// </summary>
    [HttpDelete("{filename}")]
    public async Task<IActionResult> Delete([FromRoute] string filename)
    {
        var sanitized = SanitizeFilename(filename);
        if (string.IsNullOrEmpty(sanitized))
            return BadRequest("Filename is invalid.");

        var path = Path.Combine(FilesDirectory, sanitized);
        if (!System.IO.File.Exists(path))
            return NotFound();

        System.IO.File.Delete(path);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            "/api/Site/Files/" + sanitized, "Deleted file",
            new { filename = sanitized }));
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Strips path separators and dangerous characters from a filename.
    /// Returns null if the result would be empty or still look unsafe.
    /// </summary>
    public static string SanitizeFilename(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        // Take the basename only (reject path components)
        var name = Path.GetFileName(raw);

        // Replace unsafe chars with underscores
        name = UnsafeFilenameChars.Replace(name, "_");

        // Defense in depth: reject anything that would still let you traverse
        if (name.Contains("..") || name.StartsWith('.'))
            return "";

        return name;
    }
}
