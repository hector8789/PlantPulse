using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PlantPulse.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    public sealed record HealthResponse(string Status, string Service, DateTime TimestampUtc);

    /// <summary>Verifica que la API responde (y, desde React, que CORS está bien).</summary>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() =>
        Ok(new HealthResponse("Healthy", "PlantPulse.Api", DateTime.UtcNow));
}
