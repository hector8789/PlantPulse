using PlantPulse.Application.Common;
using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.Produccion;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Application.Turnos;

/// <summary>Porcentajes como fracción 0–1 con 4 decimales; tiempos en minutos con 2 decimales.</summary>
public sealed record OeeDto(
    decimal DuracionTurnoMin,
    decimal ParosPlanificadosMin,
    decimal TiempoPlanificadoMin,
    decimal ParosNoPlanificadosMin,
    decimal TiempoOperativoMin,
    int PiezasTotales,
    int PiezasBuenas,
    int Scrap,
    decimal Disponibilidad,
    decimal Desempeno,
    decimal Calidad,
    decimal Oee,
    bool DesempenoExcedido)
{
    public static OeeDto Desde(ResultadoOee r) => new(
        Min(r.DuracionTurno), Min(r.ParosPlanificados), Min(r.TiempoPlanificado),
        Min(r.ParosNoPlanificados), Min(r.TiempoOperativo),
        r.Piezas.Totales, r.Piezas.Buenas, r.Scrap,
        Pct(r.Disponibilidad), Pct(r.Desempeno), Pct(r.Calidad), Pct(r.Oee),
        r.DesempenoExcedido);

    private static decimal Min(Duracion d) => Math.Round(d.TotalMinutos, 2, MidpointRounding.AwayFromZero);
    private static decimal Pct(Porcentaje p) => Math.Round(p.Valor, 4, MidpointRounding.AwayFromZero);
}

public sealed record RegistroProduccionDto(
    Guid Id,
    Guid OrdenProduccionId,
    string OrdenNumero,
    string MaterialCodigo,
    decimal TiempoCicloIdealSeg,
    int PiezasTotales,
    int PiezasBuenas,
    int Scrap,
    DateTimeOffset RegistradoEnUtc,
    DateTimeOffset? ModificadoEnUtc);

public sealed record ParoDto(
    Guid Id,
    Guid CausaParoId,
    string CausaCodigo,
    string CausaDescripcion,
    string Tipo,
    DateTimeOffset InicioUtc,
    DateTimeOffset FinUtc,
    decimal DuracionMin,
    string? Comentario);

public sealed record TurnoLineaDto(
    Guid Id,
    Guid PlantaId,
    Guid LineaId,
    string LineaCodigo,
    Guid TurnoId,
    int TurnoNumero,
    string TurnoNombre,
    DateOnly FechaOperativa,
    DateTimeOffset InicioUtc,
    DateTimeOffset FinUtc,
    string Estado,
    DateTimeOffset? CerradoEnUtc,
    IReadOnlyList<RegistroProduccionDto> Registros,
    IReadOnlyList<ParoDto> Paros,
    OeeDto Oee);

public sealed record TurnoResumenDto(
    Guid Id,
    Guid PlantaId,
    Guid LineaId,
    string LineaCodigo,
    Guid TurnoId,
    int TurnoNumero,
    DateOnly FechaOperativa,
    string Estado,
    DateTimeOffset InicioUtc,
    DateTimeOffset FinUtc,
    int Paros,
    OeeDto Oee);

/// <summary>Arma el detalle de un turno resolviendo nombres de línea, turno, órdenes y causas.</summary>
public sealed class TurnoLineaDtoFactory(
    IPlantaRepository plantas,
    ILineaRepository lineas,
    ICausaParoRepository causas,
    IOrdenProduccionRepository ordenes)
{
    public async Task<TurnoLineaDto> CrearAsync(TurnoLinea t, CancellationToken ct)
    {
        var planta = await plantas.ObtenerAsync(t.PlantaId, ct) ?? throw new NotFoundException("Planta", t.PlantaId);
        var linea = await lineas.ObtenerAsync(t.LineaId, ct) ?? throw new NotFoundException("Línea", t.LineaId);
        var turno = planta.ObtenerTurno(t.TurnoId);
        var catalogoCausas = (await causas.ListarAsync(ct)).ToDictionary(c => c.Id);
        var ordenesPorId = (await ordenes.ListarPorIdsAsync([.. t.Registros.Select(r => r.OrdenProduccionId).Distinct()], ct))
            .ToDictionary(o => o.Id);

        return Mapear(t, linea, turno, catalogoCausas, ordenesPorId);
    }

    public static TurnoLineaDto Mapear(
        TurnoLinea t, Linea linea, Turno turno,
        IReadOnlyDictionary<Guid, CausaParo> causasPorId, IReadOnlyDictionary<Guid, OrdenProduccion> ordenesPorId)
    {
        var registros = t.Registros
            .OrderBy(r => r.RegistradoEnUtc)
            .Select(r =>
            {
                ordenesPorId.TryGetValue(r.OrdenProduccionId, out var o);
                return new RegistroProduccionDto(
                    r.Id, r.OrdenProduccionId, o?.Numero ?? "?", o?.MaterialCodigo ?? "?", r.TiempoCicloIdeal.Segundos,
                    r.PiezasTotales, r.PiezasBuenas, r.Scrap, r.RegistradoEnUtc, r.ModificadoEnUtc);
            })
            .ToList();

        var paros = t.Paros
            .OrderBy(p => p.InicioUtc)
            .Select(p =>
            {
                causasPorId.TryGetValue(p.CausaParoId, out var c);
                return new ParoDto(
                    p.Id, p.CausaParoId, c?.Codigo ?? "?", c?.Descripcion ?? "?", p.Tipo.ToString(),
                    p.InicioUtc, p.FinUtc, Math.Round(p.Duracion.TotalMinutos, 2), p.Comentario);
            })
            .ToList();

        return new TurnoLineaDto(
            t.Id, t.PlantaId, t.LineaId, linea.Codigo, t.TurnoId, turno.Numero, turno.Nombre, t.FechaOperativa,
            t.InicioUtc, t.FinUtc, t.Estado.ToString(), t.CerradoEnUtc, registros, paros, OeeDto.Desde(t.CalcularOee()));
    }
}

internal static class TurnoAcceso
{
    /// <summary>Carga el turno con sus hijos y valida que el usuario tenga acceso a su planta (404 / 403).</summary>
    public static async Task<TurnoLinea> CargarAsync(ITurnoLineaRepository repo, ICurrentUser usuario, Guid id, CancellationToken ct)
    {
        var turno = await repo.ObtenerAsync(id, ct) ?? throw new NotFoundException("Turno de línea", id);
        usuario.AsegurarAccesoPlanta(turno.PlantaId);
        return turno;
    }
}
