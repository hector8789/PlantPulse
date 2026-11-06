using Microsoft.EntityFrameworkCore;
using PlantPulse.Application.Common;
using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.Produccion;
using PlantPulse.Domain.TiposCambio;
using PlantPulse.Domain.Usuarios;

namespace PlantPulse.Infrastructure.Persistence.Repositorios;

// Las lecturas para consulta usan AsNoTracking cuando el resultado no se va a modificar.
// Los métodos "Obtener" devuelven entidades con tracking porque los comandos las modifican.

internal sealed class PlantaRepository(PlantPulseDbContext db) : IPlantaRepository
{
    public Task<Planta?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.Plantas.Include(p => p.Turnos).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Planta>> ListarAsync(IReadOnlyCollection<Guid>? plantaIds, CancellationToken ct)
    {
        var q = db.Plantas.AsNoTracking().Include(p => p.Turnos).AsQueryable();
        if (plantaIds is not null) q = q.Where(p => plantaIds.Contains(p.Id));
        return await q.ToListAsync(ct);
    }
}

internal sealed class LineaRepository(PlantPulseDbContext db) : ILineaRepository
{
    public Task<Linea?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.Lineas.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<Linea>> ListarAsync(IReadOnlyCollection<Guid>? plantaIds, CancellationToken ct)
    {
        var q = db.Lineas.AsNoTracking();
        if (plantaIds is not null) q = q.Where(l => plantaIds.Contains(l.PlantaId));
        return await q.ToListAsync(ct);
    }
}

internal sealed class CausaParoRepository(PlantPulseDbContext db) : ICausaParoRepository
{
    public Task<CausaParo?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.CausasParo.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<CausaParo>> ListarAsync(CancellationToken ct) =>
        await db.CausasParo.AsNoTracking().ToListAsync(ct);
}

internal sealed class OrdenProduccionRepository(PlantPulseDbContext db) : IOrdenProduccionRepository
{
    public Task<OrdenProduccion?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.OrdenesProduccion.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<OrdenProduccion>> ListarPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : await db.OrdenesProduccion.AsNoTracking().Where(o => ids.Contains(o.Id)).ToListAsync(ct);

    /// <summary>Con tracking: la sincronización del ERP las actualiza.</summary>
    public async Task<IReadOnlyList<OrdenProduccion>> ListarPorNumerosAsync(IReadOnlyCollection<string> numeros, CancellationToken ct) =>
        numeros.Count == 0 ? [] : await db.OrdenesProduccion.Where(o => numeros.Contains(o.Numero)).ToListAsync(ct);

    public async Task<IReadOnlyList<OrdenProduccion>> ListarPorLineaAsync(Guid lineaId, bool soloAbiertas, CancellationToken ct)
    {
        var q = db.OrdenesProduccion.AsNoTracking().Where(o => o.LineaId == lineaId);
        if (soloAbiertas) q = q.Where(o => o.Estado != EstadoOrden.Terminada);
        return await q.ToListAsync(ct);
    }

    public void Agregar(OrdenProduccion orden) => db.OrdenesProduccion.Add(orden);
}

internal sealed class TurnoLineaRepository(PlantPulseDbContext db) : ITurnoLineaRepository
{
    public Task<TurnoLinea?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.TurnosLinea
            .Include(t => t.Registros)
            .Include(t => t.Paros)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<bool> ExisteAsync(Guid lineaId, DateOnly fechaOperativa, Guid turnoId, CancellationToken ct) =>
        db.TurnosLinea.AnyAsync(t => t.LineaId == lineaId && t.FechaOperativa == fechaOperativa && t.TurnoId == turnoId, ct);

    public async Task<IReadOnlyList<TurnoLinea>> ListarAsync(
        IReadOnlyCollection<Guid>? plantaIds, Guid? lineaId, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var q = db.TurnosLinea.AsNoTracking()
            .Include(t => t.Registros)
            .Include(t => t.Paros)
            .AsSplitQuery()
            .Where(t => t.FechaOperativa >= desde && t.FechaOperativa <= hasta);
        if (plantaIds is not null) q = q.Where(t => plantaIds.Contains(t.PlantaId));
        if (lineaId is { } id) q = q.Where(t => t.LineaId == id);
        return await q.ToListAsync(ct);
    }

    public void Agregar(TurnoLinea turno) => db.TurnosLinea.Add(turno);
}

internal sealed class UsuarioRepository(PlantPulseDbContext db) : IUsuarioRepository
{
    public Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct) =>
        db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
}

internal sealed class TipoCambioRepository(PlantPulseDbContext db) : ITipoCambioRepository
{
    public async Task<IReadOnlyList<TipoCambio>> ListarAsync(string serie, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        await db.TiposCambio.AsNoTracking()
            .Where(t => t.Serie == serie && t.Fecha >= desde && t.Fecha <= hasta)
            .OrderBy(t => t.Fecha)
            .ToListAsync(ct);

    public void Agregar(TipoCambio tipoCambio) => db.TiposCambio.Add(tipoCambio);
}
