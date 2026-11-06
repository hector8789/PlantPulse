using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.Produccion;
using PlantPulse.Domain.TiposCambio;
using PlantPulse.Domain.Usuarios;

namespace PlantPulse.Application.Common;

// ───────────── Persistencia (Infrastructure las implementa con EF Core) ─────────────
// En los filtros, plantaIds == null significa "todas las plantas".

public interface IUnitOfWork
{
    /// <summary>Guarda y después publica los eventos de dominio. Lanza ConflictException ante duplicados.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPlantaRepository
{
    Task<Planta?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Planta>> ListarAsync(IReadOnlyCollection<Guid>? plantaIds, CancellationToken ct);
}

public interface ILineaRepository
{
    Task<Linea?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Linea>> ListarAsync(IReadOnlyCollection<Guid>? plantaIds, CancellationToken ct);
}

public interface ICausaParoRepository
{
    Task<CausaParo?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CausaParo>> ListarAsync(CancellationToken ct);
}

public interface IOrdenProduccionRepository
{
    Task<OrdenProduccion?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<OrdenProduccion>> ListarPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<OrdenProduccion>> ListarPorNumerosAsync(IReadOnlyCollection<string> numeros, CancellationToken ct);
    Task<IReadOnlyList<OrdenProduccion>> ListarPorLineaAsync(Guid lineaId, bool soloAbiertas, CancellationToken ct);
    void Agregar(OrdenProduccion orden);
}

public interface ITurnoLineaRepository
{
    /// <summary>Carga el agregado completo (registros y paros).</summary>
    Task<TurnoLinea?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteAsync(Guid lineaId, DateOnly fechaOperativa, Guid turnoId, CancellationToken ct);
    Task<IReadOnlyList<TurnoLinea>> ListarAsync(
        IReadOnlyCollection<Guid>? plantaIds, Guid? lineaId, DateOnly desde, DateOnly hasta, CancellationToken ct);
    void Agregar(TurnoLinea turno);
}

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct);
}

public interface ITipoCambioRepository
{
    /// <summary>Ordenados por fecha ascendente.</summary>
    Task<IReadOnlyList<TipoCambio>> ListarAsync(string serie, DateOnly desde, DateOnly hasta, CancellationToken ct);
    void Agregar(TipoCambio tipoCambio);
}

// ───────────── Seguridad (la API genera el JWT; Infrastructure hashea) ─────────────

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verificar(string password, string hash);
}

public sealed record TokenGenerado(string AccessToken, DateTimeOffset ExpiresAtUtc);

public interface IJwtTokenGenerator
{
    TokenGenerado Generar(Usuario usuario, IReadOnlyCollection<Guid> plantaIds);
}

// ───────────── Servicios externos (Infrastructure, con resiliencia de Polly) ─────────────

/// <summary>Orden tal como la publica el ERP (OData). Estado: "REL" liberada, "TECO" cerrada técnicamente.</summary>
public sealed record OrdenErp(
    string Numero,
    string Planta,
    string CentroTrabajo,
    string Material,
    string DescripcionMaterial,
    int CantidadPlaneada,
    DateOnly InicioPlan,
    DateOnly FinPlan,
    decimal? TiempoCicloSeg,
    decimal? CostoEstandarMxn,
    string Estado)
{
    public const string EstadoLiberada = "REL";
    public const string EstadoCerrada = "TECO";
}

public interface IErpOrdenesClient
{
    /// <summary>Lanza ExternalServiceException si el ERP no responde después de los reintentos.</summary>
    Task<IReadOnlyList<OrdenErp>> ObtenerOrdenesAsync(string plantaCodigo, CancellationToken ct);
}

public sealed record TipoCambioPublicado(DateOnly Fecha, decimal PesosPorDolar);

public interface ITipoCambioProvider
{
    /// <summary>FIX de Banxico (SF43718) en el rango. Lanza ExternalServiceException si no está disponible.</summary>
    Task<IReadOnlyList<TipoCambioPublicado>> ObtenerFixAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
}
