using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Events")]
[Authorize(Policy = "Scope:Webmaster")]
public class APIEventsController(LeagueSitesContext dbContext) : ControllerBase
{
    const int MaxResults = 10000;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? type,
        [FromQuery] long? userId,
        [FromQuery] string? resource,
        [FromQuery] DateTime? since,
        [FromQuery] DateTime? until)
    {
        var query = dbContext.Events
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<EventType>(type, true, out var parsedType))
            query = query.Where(e => e.Type == parsedType);

        if (userId is not null)
            query = query.Where(e => e.UserID == userId);

        if (!string.IsNullOrEmpty(resource))
            query = query.Where(e => e.Resource.Contains(resource));

        if (since is not null)
            query = query.Where(e => e.Date >= since);

        if (until is not null)
            query = query.Where(e => e.Date <= until);

        var total = await query.CountAsync();

        var events = await query
            .OrderByDescending(e => e.Date)
            .Take(MaxResults)
            .Select(e => new
            {
                e.ID,
                Type = e.Type.ToString(),
                e.Date,
                e.UserID,
                UserName = e.User == null
                    ? null
                    : e.User.UserLogins
                        .Where(ul => ul.IsPrimary)
                        .Select(ul => ul.Name)
                        .FirstOrDefault() ?? "Unknown",
                e.Resource,
                e.Summary,
                e.Description
            })
            .ToListAsync();

        return Ok(new
        {
            total,
            truncated = total > MaxResults,
            events
        });
    }
}
