using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Traffic")]
[Authorize(Policy = "Scope:Webmaster")]
public class APITrafficController(LeagueSitesContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int days = 30)
    {
        if (days < 1) days = 1;
        if (days > 365) days = 365;

        var sinceDate = DateTime.Today.AddDays(-days).ToString("yyyy-MM-dd");

        var rows = await dbContext.TrafficDaily
            .AsNoTracking()
            .Where(t => t.Date.CompareTo(sinceDate) >= 0)
            .OrderBy(t => t.Date)
            .ToListAsync();

        return Ok(rows.Select(t => new
        {
            t.Date,
            t.TotalRequests,
            t.UniqueIPs,
            StatusCounts = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(t.StatusCounts),
            TopPaths = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(t.TopPaths),
            TopReferrers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(t.TopReferrers),
            t.UpdatedAt
        }));
    }
}
