using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlantPulse.Application.PowerBi;

namespace PlantPulse.Api.Controllers;

[ApiController]
[Route("api/powerbi")]
[Authorize]
[Produces("application/json")]
public sealed class PowerBiController(ISender sender) : ControllerBase
{
    /// <summary>Reportes disponibles (clave para pedir el embed y nombre para el menú).</summary>
    [HttpGet("reportes")]
    public async Task<ActionResult<IReadOnlyList<ReportePowerBi>>> Reportes(CancellationToken ct) =>
        Ok(await sender.Send(new ListarReportesPowerBiQuery(), ct));

    /// <summary>
    /// Embed token con RLS por planta. Sin plantaId incluye todas las plantas del usuario.
    /// El token dura ~60 min: el frontend debe pedir uno nuevo antes de expiresAtUtc.
    /// 404 si la clave no existe · 503 si Power BI no está configurado o no responde.
    /// </summary>
    [HttpGet("embed/{reportKey}")]
    [ProducesResponseType<EmbedPowerBiDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EmbedPowerBiDto>> Embed(string reportKey, [FromQuery] Guid? plantaId, CancellationToken ct) =>
        Ok(await sender.Send(new ObtenerEmbedPowerBiQuery(reportKey, plantaId), ct));
}
