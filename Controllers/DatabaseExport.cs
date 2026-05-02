using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Database")]
[Authorize(Policy = "Scope:Webmaster")]
public class APIDatabaseExportController(LeagueSitesContext dbContext) : ControllerBase
{
    /// <summary>
    /// Creates a consistent SQLite backup and streams it as a download.
    /// Uses SQLite's online backup API so the app can keep running.
    /// </summary>
    [HttpGet("Export")]
    public IActionResult Export()
    {
        var connection = dbContext.Database.GetDbConnection() as SqliteConnection;
        if (connection is null)
            return StatusCode(500, "Database connection is not SQLite.");

        // Ensure the source connection is open
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var tempPath = Path.Combine(Path.GetTempPath(), $"export-{timestamp}.db");

        try
        {
            // Use SQLite backup API for a consistent snapshot
            using (var backupConnection = new SqliteConnection($"Data Source={tempPath}"))
            {
                backupConnection.Open();
                connection.BackupDatabase(backupConnection);
            }

            // Stream the file and delete on completion
            var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.None,
                bufferSize: 81920, options: FileOptions.DeleteOnClose);

            return File(stream, "application/x-sqlite3", $"LeagueSites-{timestamp}.db");
        }
        catch
        {
            // Clean up temp file if something went wrong before streaming
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
            throw;
        }
    }
}
