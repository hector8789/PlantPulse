using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlantPulse.Api.Security;
using PlantPulse.Application.Turnos;

namespace PlantPulse.Api.Controllers;

public sealed record RegistrarProduccionRequest(Guid OrdenProduccionId, int PiezasTotales, int PiezasBuenas);
public sealed record CorregirProduccionRequest(int PiezasTotales, int PiezasBuenas);
public sealed record RegistrarParoRequest(Guid CausaParoId, DateTimeOffset InicioUtc, DateTimeOffset FinUtc, string? Comentario);

/// <summary>
/// Captura por turno de línea. Todas las acciones de escritura devuelven el turno completo
/// (registros, paros y OEE recalculado) para que el frontend solo tenga que reemplazar su estado.
/// </summary>
[ApiController]
[Route("api/turnos-linea")]
[Authorize]
[Produces("application/json")]
public sealed class TurnosLineaController(ISender sender) : ControllerBase
{
    /// <summary>Turnos en un rango de fechas operativas (máximo 62 días) con su OEE.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TurnoResumenDto>>> Listar(
        [FromQuery] Guid? plantaId, [FromQuery] Guid? lineaId, [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta,
        CancellationToken ct) =>
        Ok(await sender.Send(new ListarTurnosQuery(plantaId, lineaId, desde, hasta), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TurnoLineaDto>> Obtener(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new ObtenerTurnoQuery(id), ct));

    /// <summary>Abre el turno de una línea en una fecha operativa.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.Captura)]
    [ProducesResponseType<TurnoLineaDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TurnoLineaDto>> Abrir([FromBody] AbrirTurnoCommand command, CancellationToken ct)
    {
        var turno = await sender.Send(command, ct);
        return CreatedAtAction(nameof(Obtener), new { id = turno.Id }, turno);
    }

    [HttpPost("{id:guid}/produccion")]
    [Authorize(Policy = Politicas.Captura)]
    public async Task<ActionResult<TurnoLineaDto>> RegistrarProduccion(Guid id, [FromBody] RegistrarProduccionRequest body, CancellationToken ct) =>
        Ok(await sender.Send(new RegistrarProduccionCommand(id, body.OrdenProduccionId, body.PiezasTotales, body.PiezasBuenas), ct));

    [HttpPut("{id:guid}/produccion/{registroId:guid}")]
    [Authorize(Policy = Politicas.Captura)]
    public async Task<ActionResult<TurnoLineaDto>> CorregirProduccion(
        Guid id, Guid registroId, [FromBody] CorregirProduccionRequest body, CancellationToken ct) =>
        Ok(await sender.Send(new CorregirProduccionCommand(id, registroId, body.PiezasTotales, body.PiezasBuenas), ct));

    [HttpPost("{id:guid}/paros")]
    [Authorize(Policy = Politicas.Captura)]
    public async Task<ActionResult<TurnoLineaDto>> RegistrarParo(Guid id, [FromBody] RegistrarParoRequest body, CancellationToken ct) =>
        Ok(await sender.Send(new RegistrarParoCommand(id, body.CausaParoId, body.InicioUtc, body.FinUtc, body.Comentario), ct));

    [HttpDelete("{id:guid}/paros/{paroId:guid}")]
    [Authorize(Policy = Politicas.Captura)]
    public async Task<ActionResult<TurnoLineaDto>> EliminarParo(Guid id, Guid paroId, CancellationToken ct) =>
        Ok(await sender.Send(new EliminarParoCommand(id, paroId), ct));

    /// <summary>Cierra el turno (solo después de su hora de fin). Queda inmutable.</summary>
    [HttpPost("{id:guid}/cerrar")]
    [Authorize(Policy = Politicas.Captura)]
    public async Task<ActionResult<TurnoLineaDto>> Cerrar(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new CerrarTurnoCommand(id), ct));

    /// <summary>Reabre un turno cerrado. Solo Admin.</summary>
    [HttpPost("{id:guid}/reabrir")]
    [Authorize(Policy = Politicas.SoloAdmin)]
    public async Task<ActionResult<TurnoLineaDto>> Reabrir(Guid id, CancellationToken ct) =>
        Ok(await sender.Send(new ReabrirTurnoCommand(id), ct));
}
