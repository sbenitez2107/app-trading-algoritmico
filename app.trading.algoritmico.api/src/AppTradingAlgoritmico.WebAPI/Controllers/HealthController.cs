using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace AppTradingAlgoritmico.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Returns the current health status of the API.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        return Ok(new
        {
            status = "Healthy",
            service = "AppTradingAlgoritmico.API",
            version,
            timestamp = DateTime.UtcNow,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }
}
