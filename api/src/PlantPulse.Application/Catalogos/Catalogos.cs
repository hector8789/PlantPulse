using MediatR;
using PlantPulse.Application.Common;

namespace PlantPulse.Application.Catalogos;

// ───────────── DTOs ─────────────

public sealed record TurnoDefinicionDto(Guid Id, int Numero, string Nombre, string HoraInicio, string HoraFin);

public sealed record PlantaDto(Guid Id, string Codigo, string Nombre, string ZonaHoraria, IReadOnlyList<TurnoDefinicionDto> Turnos);

public sealed record LineaDto(Guid Id, Guid PlantaId, string Codigo, string Nombre, decimal TiempoCicloEstandarSeg, bool Activa);

public sealed record CausaParoDto(Guid Id, string Codigo, string Descripcion, string Tipo, string Categoria, bool Activa);

public sealed record OrdenDto(
    Guid Id,
    string Numero,
    Guid LineaId,
    string MaterialCodigo,
    string MaterialDescripcion,
    int CantidadPlaneada,
    DateOnly FechaInicioPlan,
    DateOnly FechaFinPlan,
    decimal TiempoCicloIdealSeg,
    bool TiempoCicloPropio,
    decimal? CostoUnitarioMxn,
    string Estado);

// ───────────── Queries ─────────────

/// <summary>Plantas visibles para el usuario, con sus turnos.</summary>
public sealed record ObtenerPlantasQuery : IRequest<IReadOnlyList<PlantaDto>>;

public sealed class ObtenerPlantasHandler(IPlantaRepository plantas, ICurrentUser usuario)
    : IRequestHandler<ObtenerPlantasQuery, IReadOnlyList<PlantaDto>>
{
    public async Task<IReadOnlyList<PlantaDto>> Handle(ObtenerPlantasQuery request, CancellationToken ct)
    {
        var lista = await plantas.ListarAsync(usuario.PlantasVisibles(null), ct);
        return lista
            .OrderBy(p => p.Codigo)
            .Select(p => new PlantaDto(p.Id, p.Codigo, p.Nombre, p.ZonaHoraria,
                p.Turnos.OrderBy(t => t.Numero)
                    .Select(t => new TurnoDefinicionDto(t.Id, t.Numero, t.Nombre, t.HoraInicio.ToString("HH\\:mm"), t.HoraFin.ToString("HH\\:mm")))
                    .ToList()))
            .ToList();
    }
}

public sealed record ObtenerLineasQuery(Guid? PlantaId) : IRequest<IReadOnlyList<LineaDto>>;

public sealed class ObtenerLineasHandler(ILineaRepository lineas, ICurrentUser usuario)
    : IRequestHandler<ObtenerLineasQuery, IReadOnlyList<LineaDto>>
{
    public async Task<IReadOnlyList<LineaDto>> Handle(ObtenerLineasQuery request, CancellationToken ct)
    {
        var lista = await lineas.ListarAsync(usuario.PlantasVisibles(request.PlantaId), ct);
        return lista.OrderBy(l => l.Codigo)
            .Select(l => new LineaDto(l.Id, l.PlantaId, l.Codigo, l.Nombre, l.TiempoCicloEstandar.Segundos, l.Activa))
            .ToList();
    }
}

public sealed record ObtenerCausasParoQuery : IRequest<IReadOnlyList<CausaParoDto>>;

public sealed class ObtenerCausasParoHandler(ICausaParoRepository causas)
    : IRequestHandler<ObtenerCausasParoQuery, IReadOnlyList<CausaParoDto>>
{
    public async Task<IReadOnlyList<CausaParoDto>> Handle(ObtenerCausasParoQuery request, CancellationToken ct) =>
        (await causas.ListarAsync(ct))
            .OrderBy(c => c.Tipo).ThenBy(c => c.Codigo)
            .Select(c => new CausaParoDto(c.Id, c.Codigo, c.Descripcion, c.Tipo.ToString(), c.Categoria.ToString(), c.Activa))
            .ToList();
}

/// <summary>Órdenes de una línea. Con SoloAbiertas devuelve las que aceptan producción (para la captura).</summary>
public sealed record ObtenerOrdenesLineaQuery(Guid LineaId, bool SoloAbiertas) : IRequest<IReadOnlyList<OrdenDto>>;

public sealed class ObtenerOrdenesLineaHandler(ILineaRepository lineas, IOrdenProduccionRepository ordenes, ICurrentUser usuario)
    : IRequestHandler<ObtenerOrdenesLineaQuery, IReadOnlyList<OrdenDto>>
{
    public async Task<IReadOnlyList<OrdenDto>> Handle(ObtenerOrdenesLineaQuery request, CancellationToken ct)
    {
        var linea = await lineas.ObtenerAsync(request.LineaId, ct) ?? throw new NotFoundException("Línea", request.LineaId);
        usuario.AsegurarAccesoPlanta(linea.PlantaId);

        var lista = await ordenes.ListarPorLineaAsync(linea.Id, request.SoloAbiertas, ct);
        return lista.OrderBy(o => o.FechaInicioPlan).ThenBy(o => o.Numero)
            .Select(o => new OrdenDto(
                o.Id, o.Numero, o.LineaId, o.MaterialCodigo, o.MaterialDescripcion, o.CantidadPlaneada,
                o.FechaInicioPlan, o.FechaFinPlan, o.ResolverTiempoCiclo(linea).Segundos, o.TiempoCicloIdeal is not null,
                o.CostoUnitarioMxn, o.Estado.ToString()))
            .ToList();
    }
}
