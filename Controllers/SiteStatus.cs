using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/SiteStatus")]
public class APISiteStatusController(IWebHostEnvironment env) : ControllerBase
{
    const string HealthFileName = "smoke-test.json";

    [HttpHead]
    public IActionResult Head()
    {
        var health = GetDeploymentHealth();
        return health.Healthy ? Ok() : StatusCode(503);
    }

    [HttpGet]
    public IActionResult Get()
    {
        var health = GetDeploymentHealth();
        var statusCode = health.Healthy ? 200 : 503;
        return StatusCode(statusCode, new
        {
            Online = true,
            health.Healthy,
            health.LastDeploy,
            health.FailedEndpoints,
        });
    }

    DeploymentHealth GetDeploymentHealth()
    {
        try
        {
            // smoke-test.json lives in the site's working directory
            var path = Path.Combine(env.ContentRootPath, HealthFileName);
            if (!System.IO.File.Exists(path))
                return new(true, null, []);

            var json = System.IO.File.ReadAllText(path);
            var result = System.Text.Json.JsonSerializer.Deserialize<SmokeTestResult>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result is null)
                return new(true, null, []);

            return new(
                result.Failed.Count == 0,
                result.Timestamp,
                result.Failed);
        }
        catch
        {
            // If we can't read the file, don't degrade — the site itself is running
            return new(true, null, []);
        }
    }

    record DeploymentHealth(bool Healthy, string? LastDeploy, List<string> FailedEndpoints);
    record SmokeTestResult(string Timestamp, List<string> Failed);
}
