using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/Site/Config")]
public class APISiteConfigController(IConfiguration config) : ControllerBase
{
    [ResponseCache(Duration = 300)]
    [HttpGet]
    public IActionResult Get()
    {
        var homeSection = config.GetSection("Site:Home");
        var socials = config.GetSection("Site:Home:Socials").GetChildren()
            .Where(s => !string.IsNullOrEmpty(s.Value))
            .ToDictionary(s => s.Key, s => s.Value!);

        return Ok(new
        {
            siteName = config["Site:Name"],
            shortName = config["Site:ShortName"],
            home = new
            {
                aboutBlurb = homeSection["AboutBlurb"],
                executives = config.GetSection("Site:Home:Executives").GetChildren()
                    .ToDictionary(e => e.Key, e => e.Value!),
                socials,
                links = config.GetSection("Site:Home:Links").GetChildren()
                    .ToDictionary(l => l.Key, l => l.Value!),
                information = config.GetSection("Site:Home:Information").GetChildren()
                    .ToDictionary(i => i.Key, i => i.Value!)
            },
            apiKeys = new
            {
                googleMaps = config["APIKeys:GoogleMaps"]
            }
        });
    }
}
