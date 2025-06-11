using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api")]
public class APISiteStatusController() : ControllerBase
{
    [HttpGet("SiteStatus")]
    public IActionResult OnGet()
    {
        var siteStatus = new Dictionary<string, bool>()
        {
            { "Online", true },
        };
        return new JsonResult(siteStatus);
    }
}