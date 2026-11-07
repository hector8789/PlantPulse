using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using PlantPulse.Application.Common;
using PlantPulse.Application.PowerBi;
using PlantPulse.Domain.Produccion;

namespace PlantPulse.Application.Turnos;

// ───────────── Abrir turno ─────────────

public sealed record AbrirTurnoCommand(Guid LineaId, Guid TurnoId, DateOnly FechaOperativa) : IRequest<TurnoLineaDto>;

public sealed class AbrirTurnoValidator : AbstractValidator<AbrirTurnoCommand>
{
    public AbrirTurnoValidator()
    {
        RuleFor(x => x.LineaId).NotEmpty().WithMessage("La línea es obligatoria.");
        RuleFor(x => x.TurnoId).NotEmpty().WithMessage("El turno es obligatorio.");
        RuleFor(x => x.FechaOperativa).NotEmpty().WithMessage("La fecha operativa es obligatoria.");
    }
}

public sealed class AbrirTurnoHandler(
    IPlantaRepository plantas,
    ILineaRepository lineas,
    ITurnoLineaRepository turnos,
    IUnitOfWork uow,
    ICurrentUser usuario,
    TimeProvider reloj,
    TurnoLineaDtoFactory dtos) : IRequestHandler<AbrirTurnoCommand, TurnoLineaDto>
{
    public async Task<TurnoLineaDto> Handle(AbrirTurnoCommand request, CancellationToken ct)
    {
        var linea = await lineas.ObtenerAsync(request.LineaId, ct) ?? throw new NotFoundException("Línea", request.LineaId);
        usuario.AsegurarAccesoPlanta(linea.PlantaId);
        var planta = await plantas.ObtenerAsync(linea.PlantaId, ct) ?? throw new NotFoundException("Planta", linea.PlantaId);

        if (await turnos.ExisteAsync(linea.Id, request.FechaOperativa, request.TurnoId, ct))
            throw new ConflictException("turno_linea.ya_existe",
                $"La línea {linea.Codigo} ya tiene abierto ese turno para {request.FechaOperativa:yyyy-MM-dd}.");

        var turno = TurnoLinea.Abrir(planta, linea, request.TurnoId, request.FechaOperativa, reloj.GetUtcNow());
        turnos.Agregar(turno);
        await uow.SaveChangesAsync(ct);
        return await dtos.CrearAsync(turno, ct);
    }
}

// ───────────── Producción ─────────────

public sealed record RegistrarProduccionCommand(Guid TurnoLineaId, Guid OrdenProduccionId, int PiezasTotales, int PiezasBuenas)
    : IRequest<TurnoLineaDto>;

public sealed class RegistrarProduccionValidator : AbstractValidator<RegistrarProduccionCommand>
{
    public RegistrarProduccionValidator()
    {
        RuleFor(x => x.TurnoLineaId).NotEmpty();
        RuleFor(x => x.OrdenProduccionId).NotEmpty().WithMessage("La orden de producción es obligatoria.");
        RuleFor(x => x.PiezasTotales).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000);
        RuleFor(x => x.PiezasBuenas).GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(x => x.PiezasTotales).WithMessage("Las piezas buenas no pueden ser más que las totales.");
    }
}

public sealed class RegistrarProduccionHandler(
    ITurnoLineaRepository turnos,
    ILineaRepository lineas,
    IOrdenProduccionRepository ordenes,
    IUnitOfWork uow,
    ICurrentUser usuario,
    TimeProvider reloj,
    TurnoLineaDtoFactory dtos) : IRequestHandler<RegistrarProduccionCommand, TurnoLineaDto>
{
    public async Task<TurnoLineaDto> Handle(RegistrarProduccionCommand request, CancellationToken ct)
    {
        var turno = await TurnoAcceso.CargarAsync(turnos, usuario, request.TurnoLineaId, ct);
        var orden = await ordenes.ObtenerAsync(request.OrdenProduccionId, ct)
            ?? throw new NotFoundException("Orden de producción", request.OrdenProduccionId);
        var linea = await lineas.ObtenerAsync(turno.LineaId, ct) ?? throw new NotFoundException("Línea", turno.LineaId);

        turno.RegistrarProduccion(orden, linea, request.PiezasTotales, request.PiezasBuenas, reloj.GetUtcNow());
        if (orden.Estado == Domain.Ordenes.EstadoOrden.Liberada) orden.Iniciar();

        await uow.SaveChangesAsync(ct);
        return await dtos.CrearAsync(turno, ct);
    }
}

public sealed record CorregirProduccionCommand(Guid TurnoLineaId, Guid RegistroId, int PiezasTotales, int PiezasBuenas)
    : IRequest<TurnoLineaDto>;

public sealed class CorregirProduccionValidator : AbstractValidator<CorregirProduccionCommand>
{
    public CorregirProduccionValidator()
    {
        RuleFor(x => x.PiezasTotales).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000);
        RuleFor(x => x.PiezasBuenas).GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(x => x.PiezasTotales).WithMessage("Las piezas buenas no pueden ser más que las totales.");
    }
}

public sealed class CorregirProduccionHandler(
    ITurnoLineaRepository turnos, IUnitOfWork uow, ICurrentUser usuario, TimeProvider reloj, TurnoLineaDtoFactory dtos)
    : IRequestHandler<CorregirProduccionCommand, TurnoLineaDto>
{
    public async Task<TurnoLineaDto> Handle(CorregirProduccionCommand request, CancellationToken ct)
    {
        var turno = await TurnoAcceso.CargarAsync(turnos, usuario, request.TurnoLineaId, ct);
        turno.CorregirProduccion(request.RegistroId, request.PiezasTotales, request.PiezasBuenas, reloj.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return await dtos.CrearAsync(turno, ct);
    }
}

// ───────────── Paros ─────────────

public sealed record RegistrarParoCommand(
    Guid TurnoLineaId, Guid CausaParoId, DateTimeOffset InicioUtc, DateTimeOffset FinUtc, string? Comentario)
    : IRequest<TurnoLineaDto>;

public sealed class RegistrarParoValidator : AbstractValidator<RegistrarParoCommand>
{
    public RegistrarParoValidator()
    {
        RuleFor(x => x.CausaParoId).NotEmpty().WithMessage("La causa del paro es obligatoria.");
        RuleFor(x => x.InicioUtc).NotEmpty().WithMessage("El inicio es obligatorio.");
        RuleFor(x => x.FinUtc).NotEmpty().WithMessage("El fin es obligatorio.")
            .GreaterThan(x => x.InicioUtc).WithMessage("El fin debe ser posterior al inicio.");
        RuleFor(x => x.Comentario).MaximumLength(TurnoLinea.MaximoComentario);
    }
}

public sealed class RegistrarParoHandler(
    ITurnoLineaRepository turnos,
    ICausaParoRepository causas,
    IUnitOfWork uow,
    ICurrentUser usuario,
    TimeProvider reloj,
    TurnoLineaDtoFactory dtos) : IRequestHandler<RegistrarParoCommand, TurnoLineaDto>
{
    public async Task<TurnoLineaDto> Handle(RegistrarParoCommand request, CancellationToken ct)
    {
        var turno = await TurnoAcceso.CargarAsync(turnos, usuario, request.TurnoLineaId, ct);
        var causa = await causas.ObtenerAsync(request.CausaParoId, ct) ?? throw new NotFoundException("Causa de paro", request.CausaParoId);

        turno.RegistrarParo(causa, request.InicioUtc, request.FinUtc, request.Comentario, reloj.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return await dtos.CrearAsync(turno, ct);
    }
}

public sealed record EliminarParoCommand(Guid TurnoLineaId, Guid ParoId) : IRequest<TurnoLineaDto>;

public sealed class EliminarParoHandler(
    ITurnoLineaRepository turnos, IUnitOfWork uow, ICurrentUser usuario, TurnoLineaDtoFactory dtos)
    : IRequestHandler<EliminarParoCommand, TurnoLineaDto>
{
    public async Task<TurnoLineaDto> Handle(EliminarParoCommand request, CancellationToken ct)
    {
        var turno = await TurnoAcceso.CargarAsync(turnos, usuario, request.TurnoLineaId, ct);
        turno.EliminarParo(request.ParoId);
        await uow.SaveChangesAsync(ct);
        return await dtos.CrearAsync(turno, ct);
    }
}

// ───────────── Cierre y reapertura ─────────────

public sealed record CerrarTurnoCommand(Guid TurnoLineaId) : IRequest<TurnoLineaDto>;

public sealed class CerrarTurnoHandler(
    ITurnoLineaRepository turnos,
    IUnitOfWork uow,
    ICurrentUser usuario,
    TimeProvider reloj,
    TurnoLineaDtoFactory dtos,
    ILogger<CerrarTurnoHandler> logger) : IRequestHandler<CerrarTurnoCommand, TurnoLineaDto>
{
    public async Task<TurnoLineaDto> Handle(CerrarTurnoCommand request, CancellationToken ct)
    {
        var turno = await TurnoAcceso.CargarAsync(turnos, usuario, request.TurnoLineaId, ct);
        turno.Cerrar(reloj.GetUtcNow());

        var oee = turno.CalcularOee();
        if (oee.DesempenoExcedido)
            logger.LogWarning(
                "Turno {TurnoLineaId}: desempeño calculado {Desempeno:P1} supera 100%; se limitó. Revisar ciclo ideal o conteo.",
                turno.Id, oee.DesempenoCalculado);

        await uow.SaveChangesAsync(ct);   // publica TurnoLineaCerrado
        return await dtos.CrearAsync(turno, ct);
    }
}

public sealed record ReabrirTurnoCommand(Guid TurnoLineaId) : IRequest<TurnoLineaDto>;

public sealed class ReabrirTurnoHandler(
    ITurnoLineaRepository turnos, IUnitOfWork uow, ICurrentUser usuario, TimeProvider reloj, TurnoLineaDtoFactory dtos)
    : IRequestHandler<ReabrirTurnoCommand, TurnoLineaDto>
{
    public async Task<TurnoLineaDto> Handle(ReabrirTurnoCommand request, CancellationToken ct)
    {
        // La API ya exige el rol Admin; se revisa aquí también para que la regla no dependa del controller.
        if (!usuario.EsAdmin()) throw new ForbiddenAccessException("Solo un Admin puede reabrir un turno.");

        var turno = await TurnoAcceso.CargarAsync(turnos, usuario, request.TurnoLineaId, ct);
        turno.Reabrir(reloj.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        return await dtos.CrearAsync(turno, ct);
    }
}

/// <summary>
/// Al cerrar un turno se pide el refresh del dataset de Power BI. Se agrupa: los cierres que llegan juntos
/// quedan en un solo refresh pendiente, que se lanza en un cierre o una apertura de reporte posterior.
/// </summary>
public sealed class TurnoLineaCerradoHandler(IPowerBiRefreshScheduler refresh, ILogger<TurnoLineaCerradoHandler> logger)
    : INotificationHandler<DomainEventNotification<TurnoLineaCerrado>>
{
    public async Task Handle(DomainEventNotification<TurnoLineaCerrado> notification, CancellationToken ct)
    {
        var e = notification.Evento;
        logger.LogInformation("Turno {TurnoLineaId} cerrado (planta {PlantaId}, {Fecha}); se solicita refresh de Power BI.",
            e.TurnoLineaId, e.PlantaId, e.FechaOperativa);
        await refresh.SolicitarAsync($"Cierre del turno {e.TurnoLineaId}", ct);
    }
}
