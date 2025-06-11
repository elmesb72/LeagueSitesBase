using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api")]
public class APISiteStatusController() : ControllerBase
{
    [HttpHead("SiteStatus")]
    public IActionResult OnHead()
    {
        var siteStatus = new Dictionary<string, bool>()
        {
            { "Online", true },
        };
        return new JsonResult(siteStatus);
    }
}