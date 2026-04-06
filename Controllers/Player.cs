using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/Player")]
public class APIPlayerController(LeagueSitesContext dbContext) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<IActionResult> Get([FromRoute] string code)
    {
        var player = await dbContext.Players
            .AsNoTracking()
            .Include(p => p.Invitations)
                .ThenInclude(i => i.Team)
            .Include(p => p.Invitations)
                .ThenInclude(i => i.User)
            .Include(p => p.Bio)
            .Include(p => p.Socials)
                .ThenInclude(s => s.Platform)
            .FirstOrDefaultAsync(p => p.ShortCode == code);

        if (player is null)
            return NotFound();

        Player? referredBy = null;
        if (player.Bio != null && !string.IsNullOrEmpty(player.Bio.ReferredBy))
        {
            referredBy = await dbContext.Players
                .FirstOrDefaultAsync(p => p.FirstName + " " + p.LastName == player.Bio.ReferredBy);
        }

        return Ok(new { player = new PlayerDetailDto(player), referredBy = referredBy != null ? new PlayerSummaryDto(referredBy) : null });
    }
}
