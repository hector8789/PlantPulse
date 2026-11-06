using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using PlantPulse.Application.Common;
using PlantPulse.Domain.Common;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Application.Erp;

public sealed record OrdenOmitidaDto(string Numero, string Motivo);

public sealed record SincronizacionOrdenesDto(
    Guid PlantaId,
    string PlantaCodigo,
    int Recibidas,
    int Creadas,
    int Actualizadas,
    int SinCambios,
    IReadOnlyList<OrdenOmitidaDto> Omitidas,
    DateTimeOffset SincronizadoEnUtc);

/// <summary>
/// Trae las órdenes de producción del ERP (OData) para una planta y las crea o actualiza.
/// Una orden con error no detiene la sincronización: se reporta en Omitidas.
/// </summary>
public sealed record SincronizarOrdenesCommand(Guid PlantaId) : IRequest<SincronizacionOrdenesDto>;

public sealed class SincronizarOrdenesValidator : AbstractValidator<SincronizarOrdenesCommand>
{
    public SincronizarOrdenesValidator() =>
        RuleFor(x => x.PlantaId).NotEmpty().WithMessage("La planta es obligatoria.");
}

public sealed class SincronizarOrdenesHandler(
    IPlantaRepository plantas,
    ILineaRepository lineas,
    IOrdenProduccionRepository ordenes,
    IErpOrdenesClient erp,
    IUnitOfWork uow,
    ICurrentUser usuario,
    TimeProvider reloj,
    ILogger<SincronizarOrdenesHandler> logger) : IRequestHandler<SincronizarOrdenesCommand, SincronizacionOrdenesDto>
{
    public async Task<SincronizacionOrdenesDto> Handle(SincronizarOrdenesCommand request, CancellationToken ct)
    {
        usuario.AsegurarAccesoPlanta(request.PlantaId);
        var planta = await plantas.ObtenerAsync(request.PlantaId, ct) ?? throw new NotFoundException("Planta", request.PlantaId);

        var deErp = await erp.ObtenerOrdenesAsync(planta.Codigo, ct);
        var lineasPorCodigo = (await lineas.ListarAsync([planta.Id], ct)).ToDictionary(l => l.Codigo, StringComparer.OrdinalIgnoreCase);
        var existentes = (await ordenes.ListarPorNumerosAsync([.. deErp.Select(o => o.Numero).Distinct()], ct))
            .ToDictionary(o => o.Numero);

        int creadas = 0, actualizadas = 0, sinCambios = 0;
        var omitidas = new List<OrdenOmitidaDto>();

        foreach (var o in deErp)
        {
            try
            {
                if (!string.Equals(o.Planta, planta.Codigo, StringComparison.OrdinalIgnoreCase))
                {
                    omitidas.Add(new(o.Numero, $"Pertenece a la planta {o.Planta}."));
                    continue;
                }
                if (!lineasPorCodigo.TryGetValue(o.CentroTrabajo, out var linea))
                {
                    omitidas.Add(new(o.Numero, $"El centro de trabajo {o.CentroTrabajo} no existe en PlantPulse."));
                    continue;
                }

                var ciclo = o.TiempoCicloSeg is { } seg ? TiempoCiclo.DeSegundos(seg) : null;
                var cerradaEnErp = string.Equals(o.Estado, OrdenErp.EstadoCerrada, StringComparison.OrdinalIgnoreCase);

                if (existentes.TryGetValue(o.Numero, out var existente))
                {
                    if (existente.LineaId != linea.Id)
                    {
                        omitidas.Add(new(o.Numero, "El ERP la movió de línea; no se soporta cambiar la línea de una orden."));
                        continue;
                    }
                    if (existente.Estado == EstadoOrden.Terminada)
                    {
                        sinCambios++;
                        continue;
                    }

                    var cambio = existente.ActualizarPlan(o.CantidadPlaneada, o.InicioPlan, o.FinPlan, ciclo, o.CostoEstandarMxn);
                    if (cerradaEnErp)
                    {
                        existente.Terminar();
                        cambio = true;
                    }
                    if (cambio) actualizadas++; else sinCambios++;
                }
                else
                {
                    if (cerradaEnErp)
                    {
                        omitidas.Add(new(o.Numero, "Ya está cerrada técnicamente (TECO) en el ERP."));
                        continue;
                    }

                    var nueva = OrdenProduccion.Crear(
                        o.Numero, linea, o.Material, o.DescripcionMaterial, o.CantidadPlaneada,
                        o.InicioPlan, o.FinPlan, ciclo, o.CostoEstandarMxn);
                    ordenes.Agregar(nueva);
                    existentes[nueva.Numero] = nueva;
                    creadas++;
                }
            }
            catch (DomainException ex)
            {
                omitidas.Add(new(o.Numero, ex.Message));
            }
        }

        await uow.SaveChangesAsync(ct);
        logger.LogInformation(
            "Sincronización ERP {Planta}: {Recibidas} recibidas, {Creadas} creadas, {Actualizadas} actualizadas, {Omitidas} omitidas.",
            planta.Codigo, deErp.Count, creadas, actualizadas, omitidas.Count);

        return new SincronizacionOrdenesDto(
            planta.Id, planta.Codigo, deErp.Count, creadas, actualizadas, sinCambios, omitidas, reloj.GetUtcNow());
    }
}
