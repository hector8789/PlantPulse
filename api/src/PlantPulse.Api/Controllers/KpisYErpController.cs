using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlantPulse.Api.Security;
using PlantPulse.Application.Erp;
using PlantPulse.Application.Kpis;

namespace PlantPulse.Api.Controllers;

[ApiController]
[Route("api/kpis")]
[Authorize]
[Produces("application/json")]
public sealed class KpisController(ISender sender) : ControllerBase
{
    /// <summary>OEE agregado (total, por línea y por línea + turno). Rango máximo 92 días.</summary>
    [HttpGet("oee")]
    public async Task<ActionResult<OeeResumenDto>> Oee(
        [FromQuery] Guid? plantaId, [FromQuery] Guid? lineaId, [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta,
        CancellationToken ct) =>
        Ok(await sender.Send(new ObtenerOeeQuery(plantaId, lineaId, desde, hasta), ct));

    /// <summary>Costo del scrap en MXN y USD (FIX de Banxico vigente para cada fecha). 503 si Banxico no responde y no hay caché.</summary>
    [HttpGet("costo-scrap")]
    public async Task<ActionResult<CostoScrapDto>> CostoScrap(
        [FromQuery] Guid? plantaId, [FromQuery] Guid? lineaId, [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta,
        CancellationToken ct) =>
        Ok(await sender.Send(new ObtenerCostoScrapQuery(plantaId, lineaId, desde, hasta), ct));
}

[ApiController]
[Route("api/erp")]
[Authorize(Policy = Politicas.Captura)]
[Produces("application/json")]
public sealed class ErpController(ISender sender) : ControllerBase
{
    /// <summary>Trae las órdenes del ERP (OData) para la planta y las crea o actualiza. 503 si el ERP no responde.</summary>
    [HttpPost("ordenes/sincronizar")]
    public async Task<ActionResult<SincronizacionOrdenesDto>> Sincronizar([FromBody] SincronizarOrdenesCommand command, CancellationToken ct) =>
        Ok(await sender.Send(command, ct));
}
