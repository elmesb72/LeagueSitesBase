using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api")]
public class APISiteStatusController() : ControllerBase
{
    [HttpHead("SiteStatus")]
    public void OnHead()
    {
        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
    }

    [HttpGet("SiteStatus")]
    public IActionResult OnGet()
    {
        return new JsonResult(new Dictionary<string, bool>()
        {
            { "Online", true },
        });
    }
}