using Microsoft.AspNetCore.Mvc;

namespace MallSense.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Check()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "Sunway Kluang Mall API",
            version = "1.0.0",
            dotnetVersion = Environment.Version.ToString(),
            timestampUtc = DateTime.UtcNow
        });
    }
}
