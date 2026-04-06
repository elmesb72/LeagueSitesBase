using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/SiteStatus")]
public class APISiteStatusController : ControllerBase
{
    [HttpHead]
    public IActionResult Head() => Ok();

    [HttpGet]
    public IActionResult Get() => Ok(new { Online = true });
}
