using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using PlantPulse.Application.Common;
using PlantPulse.Domain.TiposCambio;

namespace PlantPulse.Application.Kpis;

// ───────────── Tipo de cambio con caché en base de datos ─────────────

public sealed record TiposCambioVigentes(IReadOnlyList<TipoCambio> Tipos, bool Desactualizado);

/// <summary>
/// Obtiene el FIX de Banxico para un rango. Primero usa lo guardado; si falta, consulta a Banxico y guarda lo nuevo.
/// Si Banxico falla pero hay datos guardados, sigue con ellos y marca el resultado como desactualizado.
/// </summary>
public sealed class TiposCambioService(
    ITipoCambioRepository repo,
    ITipoCambioProvider banxico,
    IUnitOfWork uow,
    TimeProvider reloj,
    ILogger<TiposCambioService> logger)
{
    /// <summary>Días hacia atrás que se piden para cubrir fines de semana y festivos al inicio del rango.</summary>
    public const int MargenDias = 10;

    /// <summary>Banxico publica el FIX en días hábiles; se tolera que falten los últimos días.</summary>
    public const int ToleranciaDias = 3;

    public async Task<TiposCambioVigentes> ObtenerAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        var fin = hasta > hoy ? hoy : hasta;
        var inicio = desde.AddDays(-MargenDias);

        var guardados = await repo.ListarAsync(TipoCambio.SerieFix, inicio, fin, ct);
        if (Cubre(guardados, desde, fin)) return new TiposCambioVigentes(guardados, false);

        try
        {
            var publicados = await banxico.ObtenerFixAsync(inicio, fin, ct);
            var existentes = guardados.Select(t => t.Fecha).ToHashSet();
            var ahora = reloj.GetUtcNow();
            var nuevos = publicados
                .Where(p => !existentes.Contains(p.Fecha))
                .Select(p => TipoCambio.Crear(TipoCambio.SerieFix, p.Fecha, p.PesosPorDolar, "Banxico", ahora))
                .ToList();

            foreach (var t in nuevos) repo.Agregar(t);
            if (nuevos.Count > 0) await uow.SaveChangesAsync(ct);

            var todos = guardados.Concat(nuevos).OrderBy(t => t.Fecha).ToList();
            if (todos.Count == 0)
                throw new ExternalServiceException("Banxico", "Banxico no devolvió tipos de cambio para el rango solicitado.");
            return new TiposCambioVigentes(todos, false);
        }
        catch (ExternalServiceException ex) when (guardados.Count > 0)
        {
            logger.LogWarning(ex, "Banxico no disponible; se usan {Cantidad} tipos de cambio guardados.", guardados.Count);
            return new TiposCambioVigentes(guardados, true);
        }
    }

    private static bool Cubre(IReadOnlyList<TipoCambio> guardados, DateOnly desde, DateOnly fin) =>
        guardados.Count > 0
        && guardados[0].Fecha <= desde
        && guardados[^1].Fecha >= fin.AddDays(-ToleranciaDias);
}

// ───────────── Costo del scrap ─────────────

public sealed record CostoScrapLineaDto(Guid LineaId, string LineaCodigo, int PiezasScrap, decimal CostoMxn, decimal CostoUsd);

public sealed record CostoScrapDiaDto(DateOnly Fecha, int PiezasScrap, decimal CostoMxn, decimal TipoCambio, decimal CostoUsd);

public sealed record CostoScrapDto(
    DateOnly Desde,
    DateOnly Hasta,
    string SerieTipoCambio,
    bool TipoCambioDesactualizado,
    int PiezasScrap,
    int PiezasScrapSinCosto,
    decimal CostoMxn,
    decimal CostoUsd,
    IReadOnlyList<CostoScrapLineaDto> PorLinea,
    IReadOnlyList<CostoScrapDiaDto> PorDia);

/// <summary>
/// Costo del scrap = piezas de scrap × costo estándar de la orden (MXN).
/// En USD se divide entre el FIX vigente de la fecha operativa de cada turno.
/// </summary>
public sealed record ObtenerCostoScrapQuery(Guid? PlantaId, Guid? LineaId, DateOnly Desde, DateOnly Hasta) : IRequest<CostoScrapDto>;

public sealed class ObtenerCostoScrapValidator : AbstractValidator<ObtenerCostoScrapQuery>
{
    public ObtenerCostoScrapValidator()
    {
        RuleFor(x => x.Desde).NotEmpty().WithMessage("La fecha inicial es obligatoria.");
        RuleFor(x => x.Hasta).NotEmpty().WithMessage("La fecha final es obligatoria.")
            .GreaterThanOrEqualTo(x => x.Desde).WithMessage("La fecha final no puede ser anterior a la inicial.")
            .Must((q, hasta) => hasta.DayNumber - q.Desde.DayNumber < ObtenerOeeValidator.MaximoDias)
            .WithMessage($"El rango no puede exceder {ObtenerOeeValidator.MaximoDias} días.");
    }
}

public sealed class ObtenerCostoScrapHandler(
    ITurnoLineaRepository turnos,
    ILineaRepository lineas,
    IOrdenProduccionRepository ordenes,
    TiposCambioService tiposCambio,
    ICurrentUser usuario) : IRequestHandler<ObtenerCostoScrapQuery, CostoScrapDto>
{
    public async Task<CostoScrapDto> Handle(ObtenerCostoScrapQuery request, CancellationToken ct)
    {
        var plantasVisibles = usuario.PlantasVisibles(request.PlantaId);
        if (request.LineaId is { } lineaId)
        {
            var linea = await lineas.ObtenerAsync(lineaId, ct) ?? throw new NotFoundException("Línea", lineaId);
            usuario.AsegurarAccesoPlanta(linea.PlantaId);
        }

        var lista = await turnos.ListarAsync(plantasVisibles, request.LineaId, request.Desde, request.Hasta, ct);
        var conScrap = lista
            .SelectMany(t => t.Registros.Where(r => r.Scrap > 0).Select(r => (Turno: t, Registro: r)))
            .ToList();

        if (conScrap.Count == 0)
            return new CostoScrapDto(request.Desde, request.Hasta, TipoCambio.SerieFix, false, 0, 0, 0m, 0m, [], []);

        var ordenesPorId = (await ordenes.ListarPorIdsAsync([.. conScrap.Select(x => x.Registro.OrdenProduccionId).Distinct()], ct))
            .ToDictionary(o => o.Id);
        var lineasPorId = (await lineas.ListarAsync(plantasVisibles, ct)).ToDictionary(l => l.Id);
        var tc = await tiposCambio.ObtenerAsync(request.Desde, request.Hasta, ct);

        var renglones = conScrap.Select(x =>
        {
            var costo = ordenesPorId.TryGetValue(x.Registro.OrdenProduccionId, out var o) ? o.CostoUnitarioMxn : null;
            var tipo = TipoCambio.VigentePara(x.Turno.FechaOperativa, tc.Tipos)!.PesosPorDolar;
            var mxn = costo is { } c ? x.Registro.Scrap * c : 0m;
            return new
            {
                x.Turno.LineaId,
                x.Turno.FechaOperativa,
                x.Registro.Scrap,
                SinCosto = costo is null ? x.Registro.Scrap : 0,
                Mxn = mxn,
                Usd = mxn / tipo,
                Tipo = tipo,
            };
        }).ToList();

        var porLinea = renglones.GroupBy(r => r.LineaId)
            .Select(g => new CostoScrapLineaDto(
                g.Key, lineasPorId.TryGetValue(g.Key, out var l) ? l.Codigo : "?",
                g.Sum(r => r.Scrap), Dinero(g.Sum(r => r.Mxn)), Dinero(g.Sum(r => r.Usd))))
            .OrderByDescending(x => x.CostoMxn)
            .ToList();

        var porDia = renglones.GroupBy(r => r.FechaOperativa)
            .Select(g => new CostoScrapDiaDto(
                g.Key, g.Sum(r => r.Scrap), Dinero(g.Sum(r => r.Mxn)), g.First().Tipo, Dinero(g.Sum(r => r.Usd))))
            .OrderBy(x => x.Fecha)
            .ToList();

        return new CostoScrapDto(
            request.Desde, request.Hasta, TipoCambio.SerieFix, tc.Desactualizado,
            renglones.Sum(r => r.Scrap), renglones.Sum(r => r.SinCosto),
            Dinero(renglones.Sum(r => r.Mxn)), Dinero(renglones.Sum(r => r.Usd)),
            porLinea, porDia);
    }

    private static decimal Dinero(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
