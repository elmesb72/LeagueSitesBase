using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/News")]
public class APINewsController(
    LeagueSitesContext dbContext,
    IPermissionsService permissionsService) : ControllerBase
{
    public record NewsUpsertDto(
        long AuthorInvitationID,
        string Title,
        string Contents,
        bool IsHidden);

    [Authorize(Policy = "Scope:Reporter,Scorer,Manager,Executive,Webmaster")]
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get([FromRoute] long id)
    {
        var permissions = await permissionsService.GetAsync(User);

        var news = await dbContext.News
            .AsNoTracking()
            .Include(n => n.Author)
                .ThenInclude(u => u!.Invitations)
                    .ThenInclude(i => i.Team)
            .FirstOrDefaultAsync(n => n.ID == id);

        if (news is null)
            return NotFound();

        var canEdit = news.AuthorID == permissions.User?.ID
            || permissions.Include([PermissionsScope.Executive, PermissionsScope.Webmaster]);

        return Ok(new { news, canEdit });
    }

    [Authorize(Policy = "Scope:Reporter,Scorer,Manager,Executive,Webmaster")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] NewsUpsertDto dto)
    {
        var permissions = await permissionsService.GetAsync(User);
        if (permissions.User is null) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Contents))
            return BadRequest("Title and contents are required.");

        var news = new News
        {
            AuthorID = permissions.User.ID,
            AuthorInvitationID = dto.AuthorInvitationID,
            Date = DateTime.Now,
            Title = dto.Title,
            Contents = dto.Contents,
            Source = string.Empty,
            IsHidden = dto.IsHidden,
        };

        await dbContext.News.AddAsync(news);
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = news.ID }, news);
    }

    [Authorize(Policy = "Scope:Reporter,Scorer,Manager,Executive,Webmaster")]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update([FromRoute] long id, [FromBody] NewsUpsertDto dto)
    {
        var permissions = await permissionsService.GetAsync(User);
        if (permissions.User is null) return Forbid();

        var news = await dbContext.News.FirstOrDefaultAsync(n => n.ID == id);
        if (news is null) return NotFound();

        // Can only edit your own posts unless Executive/Webmaster
        if (news.AuthorID != permissions.User.ID
            && !permissions.Include([PermissionsScope.Executive, PermissionsScope.Webmaster]))
        {
            return Forbid();
        }

        news.AuthorInvitationID = dto.AuthorInvitationID;
        news.Title = dto.Title;
        news.Contents = dto.Contents;
        news.IsHidden = dto.IsHidden;
        news.Edited = DateTime.Now;

        dbContext.News.Update(news);
        await dbContext.SaveChangesAsync();

        return Ok(news);
    }

    /// <summary>
    /// Soft-delete (sets IsDeleted flag).
    /// </summary>
    [Authorize(Policy = "Scope:Reporter,Scorer,Manager,Executive,Webmaster")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete([FromRoute] long id)
    {
        var permissions = await permissionsService.GetAsync(User);
        if (permissions.User is null) return Forbid();

        var news = await dbContext.News.FirstOrDefaultAsync(n => n.ID == id);
        if (news is null) return NotFound();

        if (news.AuthorID != permissions.User.ID
            && !permissions.Include([PermissionsScope.Executive, PermissionsScope.Webmaster]))
        {
            return Forbid();
        }

        news.IsDeleted = true;
        dbContext.News.Update(news);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Returns deleted news posts authored by the current user (recycle bin).
    /// </summary>
    [Authorize]
    [HttpGet("RecycleBin")]
    public async Task<IActionResult> RecycleBin()
    {
        var permissions = await permissionsService.GetAsync(User);
        if (permissions.User is null) return Forbid();

        var deleted = await dbContext.News
            .AsNoTracking()
            .Include(n => n.Author)
                .ThenInclude(u => u!.UserLogins)
            .Include(n => n.Author)
                .ThenInclude(u => u!.Invitations)
                    .ThenInclude(i => i.Team)
            .Where(n => n.IsDeleted && n.AuthorID == permissions.User.ID)
            .OrderByDescending(n => n.Date)
            .ToListAsync();

        return Ok(deleted);
    }
}
