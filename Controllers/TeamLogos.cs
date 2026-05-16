using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Upload, rotate, and delete team logo images. Each team can have up to
/// 10 logo sets (current + 9 backups). The current logos live at:
///   /var/db/static/images/teams/{teamId}-lg.webp
///   /var/db/static/images/teams/{teamId}-md.webp
///   /var/db/static/images/teams/{teamId}-sm.webp
///
/// Backups are named {teamId}-{size}-{n}.webp where n is 1–9.
/// A sidecar marker file {teamId}-sm.custom (or {teamId}-sm-{n}.custom for
/// backups) indicates the small logo was uploaded individually rather than
/// auto-resized from the main logo.
/// </summary>
[ApiController]
[Route("api/Teams/{teamId:long}/Logo")]
[Authorize]
public class APITeamLogosController(
    LeagueSitesContext dbContext,
    IAuthorizationService authorizationService) : ControllerBase
{
    const string TeamsDir = "/var/db/static/images/teams";
    const int MaxBackups = 9;

    // Target dimensions match the rendered sizes in the frontend components.
    const int LgDimension = 120;
    const int MdDimension = 75;
    const int SmDimension = 24;

    static readonly string[] Sizes = ["lg", "md", "sm"];

    /// <summary>
    /// Returns the current state of logos for this team: which sizes exist,
    /// whether a custom small logo is present, and how many backups exist.
    /// </summary>
    [HttpGet("Status")]
    [AllowAnonymous]
    public IActionResult GetStatus([FromRoute] long teamId)
    {
        var hasLg = System.IO.File.Exists(LogoPath(teamId, "lg"));
        var hasMd = System.IO.File.Exists(LogoPath(teamId, "md"));
        var hasSm = System.IO.File.Exists(LogoPath(teamId, "sm"));
        var hasCustomSm = System.IO.File.Exists(CustomSmSidecarPath(teamId));
        var backupCount = CountBackups(teamId);

        return Ok(new { hasLg, hasMd, hasSm, hasCustomSm, backupCount });
    }

    /// <summary>
    /// Upload a main team logo. Resizes to lg, md, and sm variants.
    /// Rotates existing logos into backup slots. If a custom small logo
    /// exists and keepCustomSmall is true, the sm variant is not overwritten.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadMain(
        [FromRoute] long teamId,
        [FromQuery] bool keepCustomSmall,
        IFormFile file)
    {
        var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == teamId);
        if (team is null) return NotFound();

        var auth = await authorizationService.AuthorizeAsync(
            User, team,
            new TeamScopedRequirement(
                PermissionsScope.Manager, PermissionsScope.Executive, PermissionsScope.Webmaster));
        if (!auth.Succeeded) return Forbid();

        // Check backup limit
        var backups = CountBackups(teamId);
        if (backups >= MaxBackups)
            return BadRequest(
                "This team has reached the maximum of 10 logo versions (current + 9 backups). " +
                "Please delete older backups before uploading a new logo.");

        // Determine which sizes to write (i.e. which the user is replacing)
        var sizesToWrite = keepCustomSmall && System.IO.File.Exists(CustomSmSidecarPath(teamId))
            ? new[] { "lg", "md" }
            : Sizes;

        // Rotate current logos into the next backup slot. Sizes being
        // replaced are moved (the new upload takes their place); sizes
        // we're keeping are copied so the backup is a complete snapshot
        // but the current files remain in place.
        var nextSlot = backups + 1;
        RotateToBackup(teamId, nextSlot, movedSizes: sizesToWrite, allSizes: Sizes);

        // Write new logos
        Directory.CreateDirectory(TeamsDir);

        foreach (var size in sizesToWrite)
        {
            var maxDim = size switch
            {
                "lg" => LgDimension,
                "md" => MdDimension,
                "sm" => SmDimension,
                _ => throw new InvalidOperationException()
            };

            var result = await ImageProcessor.ConvertAsync(
                file, LogoPath(teamId, size), ImageOutputFormat.Webp, maxDim);

            if (!result.Success)
                return BadRequest(result.ErrorMessage);
        }

        // If we wrote sm and it's auto-generated, remove any custom sidecar
        if (sizesToWrite.Contains("sm"))
        {
            var sidecar = CustomSmSidecarPath(teamId);
            if (System.IO.File.Exists(sidecar))
                System.IO.File.Delete(sidecar);
        }

        // Audit log
        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            $"/api/Teams/{teamId}/Logo", "Uploaded team logo",
            new { teamId, originalFilename = file.FileName, sizeBytes = file.Length, keepCustomSmall }));
        await dbContext.SaveChangesAsync();

        return Ok(new
        {
            paths = sizesToWrite.Select(s => $"/images/teams/{teamId}-{s}.webp").ToArray(),
            backupSlot = nextSlot
        });
    }

    /// <summary>
    /// Upload a custom small logo (shoulder-patch style). Replaces the
    /// current sm variant outright without rotation, and drops the custom
    /// sidecar marker.
    /// </summary>
    [HttpPost("Small")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadSmall(
        [FromRoute] long teamId,
        IFormFile file)
    {
        var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == teamId);
        if (team is null) return NotFound();

        var auth = await authorizationService.AuthorizeAsync(
            User, team,
            new TeamScopedRequirement(
                PermissionsScope.Manager, PermissionsScope.Executive, PermissionsScope.Webmaster));
        if (!auth.Succeeded) return Forbid();

        Directory.CreateDirectory(TeamsDir);

        var result = await ImageProcessor.ConvertAsync(
            file, LogoPath(teamId, "sm"), ImageOutputFormat.Webp, SmDimension);

        if (!result.Success)
            return BadRequest(result.ErrorMessage);

        // Drop the custom sidecar marker
        await System.IO.File.WriteAllBytesAsync(CustomSmSidecarPath(teamId), []);

        // Audit log
        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            $"/api/Teams/{teamId}/Logo/Small", "Uploaded custom small team logo",
            new { teamId, originalFilename = file.FileName, sizeBytes = file.Length }));
        await dbContext.SaveChangesAsync();

        return Ok(new { path = $"/images/teams/{teamId}-sm.webp" });
    }

    /// <summary>
    /// Returns metadata about each backup slot for this team.
    /// </summary>
    [HttpGet("Backups")]
    public async Task<IActionResult> GetBackups([FromRoute] long teamId)
    {
        var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == teamId);
        if (team is null) return NotFound();

        var auth = await authorizationService.AuthorizeAsync(
            User, team,
            new TeamScopedRequirement(
                PermissionsScope.Manager, PermissionsScope.Executive, PermissionsScope.Webmaster));
        if (!auth.Succeeded) return Forbid();

        var backups = new List<object>();
        for (var slot = 1; slot <= MaxBackups; slot++)
        {
            // Pick the newest mtime across the size variants in this slot
            // as the "uploaded" timestamp for the backup.
            DateTime? mtime = null;
            var hasAny = false;
            foreach (var size in Sizes)
            {
                var path = BackupPath(teamId, size, slot);
                if (System.IO.File.Exists(path))
                {
                    hasAny = true;
                    var t = System.IO.File.GetLastWriteTimeUtc(path);
                    if (mtime is null || t > mtime) mtime = t;
                }
            }
            if (!hasAny) break;

            backups.Add(new
            {
                slot,
                lastModifiedUtc = mtime,
                hasCustomSm = System.IO.File.Exists(BackupSmSidecarPath(teamId, slot))
            });
        }

        return Ok(backups);
    }

    /// <summary>
    /// Restore a backup to current. The existing current logo set is
    /// rotated into a new backup slot. Fails if the team is at the
    /// maximum number of backups and the current logo set isn't empty.
    /// </summary>
    [HttpPost("Backup/{slot:int}/Restore")]
    public async Task<IActionResult> RestoreBackup(
        [FromRoute] long teamId,
        [FromRoute] int slot)
    {
        var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == teamId);
        if (team is null) return NotFound();

        var auth = await authorizationService.AuthorizeAsync(
            User, team,
            new TeamScopedRequirement(
                PermissionsScope.Manager, PermissionsScope.Executive, PermissionsScope.Webmaster));
        if (!auth.Succeeded) return Forbid();

        if (slot < 1 || slot > MaxBackups)
            return BadRequest("Slot must be between 1 and 9.");

        var totalBackups = CountBackups(teamId);
        if (slot > totalBackups)
            return NotFound("No backup in that slot.");

        var hasCurrent = Sizes.Any(s => System.IO.File.Exists(LogoPath(teamId, s)));

        // If we have a current set, we need a free backup slot to rotate
        // it into. The slot we're restoring frees up a slot for us, but
        // only if there are gaps elsewhere can we go above the max.
        if (hasCurrent && totalBackups >= MaxBackups)
            return BadRequest(
                "This team has reached the maximum of 10 logo versions. " +
                "Please delete another backup before restoring.");

        // Step 1: rotate the current logo set into a new backup slot
        // (one above the current max). The slot we're restoring will be
        // freed up after we move it into current.
        var rotateToSlot = totalBackups + 1;
        if (hasCurrent)
        {
            RotateToBackup(teamId, rotateToSlot, Sizes);
        }

        // Step 2: move the requested backup slot into current
        foreach (var size in Sizes)
        {
            var src = BackupPath(teamId, size, slot);
            var dst = LogoPath(teamId, size);
            if (System.IO.File.Exists(src))
                System.IO.File.Move(src, dst, overwrite: true);
        }

        // Move the sidecar too if present
        var srcSidecar = BackupSmSidecarPath(teamId, slot);
        var dstSidecar = CustomSmSidecarPath(teamId);
        if (System.IO.File.Exists(srcSidecar))
            System.IO.File.Move(srcSidecar, dstSidecar, overwrite: true);
        else if (System.IO.File.Exists(dstSidecar))
            System.IO.File.Delete(dstSidecar);

        // Step 3: compact backup slots so there are no gaps where the
        // restored slot used to be.
        var newTotal = hasCurrent ? rotateToSlot : totalBackups;
        for (var i = slot + 1; i <= newTotal; i++)
        {
            foreach (var size in Sizes)
            {
                var src = BackupPath(teamId, size, i);
                var dst = BackupPath(teamId, size, i - 1);
                if (System.IO.File.Exists(src))
                    System.IO.File.Move(src, dst, overwrite: true);
            }
            var sidecarSrc = BackupSmSidecarPath(teamId, i);
            var sidecarDst = BackupSmSidecarPath(teamId, i - 1);
            if (System.IO.File.Exists(sidecarSrc))
                System.IO.File.Move(sidecarSrc, sidecarDst, overwrite: true);
            else if (System.IO.File.Exists(sidecarDst))
                System.IO.File.Delete(sidecarDst);
        }

        // Audit log
        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            $"/api/Teams/{teamId}/Logo/Backup/{slot}/Restore", "Restored team logo backup",
            new { teamId, restoredSlot = slot }));
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Delete a specific backup slot (1–9). Removes all size variants and
    /// any custom sidecar for that slot, then compacts remaining backups
    /// so there are no gaps.
    /// </summary>
    [HttpDelete("Backup/{slot:int}")]
    public async Task<IActionResult> DeleteBackup(
        [FromRoute] long teamId,
        [FromRoute] int slot)
    {
        var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == teamId);
        if (team is null) return NotFound();

        var auth = await authorizationService.AuthorizeAsync(
            User, team,
            new TeamScopedRequirement(
                PermissionsScope.Manager, PermissionsScope.Executive, PermissionsScope.Webmaster));
        if (!auth.Succeeded) return Forbid();

        if (slot < 1 || slot > MaxBackups)
            return BadRequest("Slot must be between 1 and 9.");

        var totalBackups = CountBackups(teamId);
        if (slot > totalBackups)
            return NotFound("No backup in that slot.");

        // Delete the target slot
        DeleteSlotFiles(teamId, slot);

        // Compact: shift higher slots down
        for (var i = slot + 1; i <= totalBackups; i++)
        {
            foreach (var size in Sizes)
            {
                var src = BackupPath(teamId, size, i);
                var dst = BackupPath(teamId, size, i - 1);
                if (System.IO.File.Exists(src))
                    System.IO.File.Move(src, dst, overwrite: true);
            }
            // Move sidecar if present
            var srcSidecar = BackupSmSidecarPath(teamId, i);
            var dstSidecar = BackupSmSidecarPath(teamId, i - 1);
            if (System.IO.File.Exists(srcSidecar))
                System.IO.File.Move(srcSidecar, dstSidecar, overwrite: true);
            else if (System.IO.File.Exists(dstSidecar))
                System.IO.File.Delete(dstSidecar);
        }

        // Audit log
        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            $"/api/Teams/{teamId}/Logo/Backup/{slot}", "Deleted team logo backup",
            new { teamId, slot }));
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Delete the current logo set (all sizes). Does NOT promote a backup.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteCurrent([FromRoute] long teamId)
    {
        var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.ID == teamId);
        if (team is null) return NotFound();

        var auth = await authorizationService.AuthorizeAsync(
            User, team,
            new TeamScopedRequirement(
                PermissionsScope.Manager, PermissionsScope.Executive, PermissionsScope.Webmaster));
        if (!auth.Succeeded) return Forbid();

        foreach (var size in Sizes)
        {
            var path = LogoPath(teamId, size);
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }

        var sidecar = CustomSmSidecarPath(teamId);
        if (System.IO.File.Exists(sidecar))
            System.IO.File.Delete(sidecar);

        var uid = Convert.ToInt64(User.Claims.First(c => c.Type == "UserID").Value);
        dbContext.Events.Add(Event.Log(
            EventType.Update, uid,
            $"/api/Teams/{teamId}/Logo", "Deleted current team logo",
            new { teamId }));
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    // --- Helpers ---

    static string LogoPath(long teamId, string size) =>
        Path.Combine(TeamsDir, $"{teamId}-{size}.webp");

    static string BackupPath(long teamId, string size, int slot) =>
        Path.Combine(TeamsDir, $"{teamId}-{size}-{slot}.webp");

    static string CustomSmSidecarPath(long teamId) =>
        Path.Combine(TeamsDir, $"{teamId}-sm.custom");

    static string BackupSmSidecarPath(long teamId, int slot) =>
        Path.Combine(TeamsDir, $"{teamId}-sm-{slot}.custom");

    /// <summary>
    /// Counts how many backup slots currently exist by checking for the
    /// lg variant at each slot (lg is always written on main upload).
    /// </summary>
    static int CountBackups(long teamId)
    {
        var count = 0;
        for (var i = 1; i <= MaxBackups; i++)
        {
            // A backup slot exists if any size file is present
            if (System.IO.File.Exists(BackupPath(teamId, "lg", i))
                || System.IO.File.Exists(BackupPath(teamId, "md", i))
                || System.IO.File.Exists(BackupPath(teamId, "sm", i)))
                count = i;
            else
                break;
        }
        return count;
    }

    /// <summary>
    /// Rotates the current logo files into the given backup slot.
    /// Sizes in <paramref name="movedSizes"/> are moved (the caller is
    /// about to write new files in their place). Other sizes in
    /// <paramref name="allSizes"/> are copied so the backup is a
    /// complete snapshot, with the current files left in place.
    /// </summary>
    static void RotateToBackup(long teamId, int slot, string[] movedSizes, string[] allSizes)
    {
        foreach (var size in allSizes)
        {
            var current = LogoPath(teamId, size);
            if (!System.IO.File.Exists(current)) continue;

            var backup = BackupPath(teamId, size, slot);
            if (movedSizes.Contains(size))
                System.IO.File.Move(current, backup, overwrite: true);
            else
                System.IO.File.Copy(current, backup, overwrite: true);
        }

        // Sidecar travels with sm. If sm is being moved, move the sidecar.
        // If sm is being kept (copied), copy the sidecar so the backup
        // still records that the small was custom.
        var sidecar = CustomSmSidecarPath(teamId);
        if (System.IO.File.Exists(sidecar) && allSizes.Contains("sm"))
        {
            var backupSidecar = BackupSmSidecarPath(teamId, slot);
            if (movedSizes.Contains("sm"))
                System.IO.File.Move(sidecar, backupSidecar, overwrite: true);
            else
                System.IO.File.Copy(sidecar, backupSidecar, overwrite: true);
        }
    }

    /// <summary>
    /// Convenience overload: rotate every size by moving (used during
    /// restore where we always want a clean swap).
    /// </summary>
    static void RotateToBackup(long teamId, int slot, string[] sizes) =>
        RotateToBackup(teamId, slot, movedSizes: sizes, allSizes: sizes);

    /// <summary>
    /// Deletes all files for a given backup slot.
    /// </summary>
    static void DeleteSlotFiles(long teamId, int slot)
    {
        foreach (var size in Sizes)
        {
            var path = BackupPath(teamId, size, slot);
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
        var sidecar = BackupSmSidecarPath(teamId, slot);
        if (System.IO.File.Exists(sidecar))
            System.IO.File.Delete(sidecar);
    }
}
