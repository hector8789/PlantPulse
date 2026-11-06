using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlantPulse.Application.Catalogos;

namespace PlantPulse.Api.Controllers;

[ApiController]
[Route("api/catalogos")]
[Authorize]
[Produces("application/json")]
public sealed class CatalogosController(ISender sender) : ControllerBase
{
    /// <summary>Plantas visibles para el usuario, con sus turnos.</summary>
    [HttpGet("plantas")]
    public async Task<ActionResult<IReadOnlyList<PlantaDto>>> Plantas(CancellationToken ct) =>
        Ok(await sender.Send(new ObtenerPlantasQuery(), ct));

    /// <summary>Líneas visibles; opcionalmente de una planta.</summary>
    [HttpGet("lineas")]
    public async Task<ActionResult<IReadOnlyList<LineaDto>>> Lineas([FromQuery] Guid? plantaId, CancellationToken ct) =>
        Ok(await sender.Send(new ObtenerLineasQuery(plantaId), ct));

    /// <summary>Catálogo global de causas de paro.</summary>
    [HttpGet("causas-paro")]
    public async Task<ActionResult<IReadOnlyList<CausaParoDto>>> CausasParo(CancellationToken ct) =>
        Ok(await sender.Send(new ObtenerCausasParoQuery(), ct));

    /// <summary>Órdenes de una línea. soloAbiertas=true (por defecto) devuelve las que aceptan producción.</summary>
    [HttpGet("lineas/{lineaId:guid}/ordenes")]
    public async Task<ActionResult<IReadOnlyList<OrdenDto>>> Ordenes(Guid lineaId, [FromQuery] bool soloAbiertas = true, CancellationToken ct = default) =>
        Ok(await sender.Send(new ObtenerOrdenesLineaQuery(lineaId, soloAbiertas), ct));
}
