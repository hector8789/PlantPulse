using FluentValidation;
using MediatR;
using PlantPulse.Application.Common;

namespace PlantPulse.Application.PowerBi;

// ───────────── Abstracciones (Infrastructure las implementa con Microsoft.PowerBI.Api + MSAL) ─────────────

public sealed record ReportePowerBi(string Key, string Nombre);

public sealed record EmbedPowerBi(Guid ReportId, string EmbedUrl, string AccessToken, DateTimeOffset ExpiresAtUtc);

public interface IPowerBiEmbedService
{
    /// <summary>Reportes configurados (clave estable para el frontend → ReportId de Power BI).</summary>
    IReadOnlyList<ReportePowerBi> Reportes();

    /// <summary>Validación rápida, sin red ni base de datos: 503 si Power BI no está configurado, 404 si la clave no existe.</summary>
    void Validar(string reportKey);

    /// <summary>
    /// Embed token para un reporte con identidad efectiva (RLS).
    /// Lanza NotFoundException si la clave no existe y ExternalServiceException si Power BI no está configurado o falla.
    /// </summary>
    Task<EmbedPowerBi> GenerarAsync(string reportKey, string usernameRls, CancellationToken ct);
}

/// <summary>
/// Refresh del dataset con estado persistido en la base (sirve en Lambda y con varias instancias).
/// Las implementaciones nunca lanzan por fallas de Power BI o de la base: solo registran el error.
/// </summary>
public interface IPowerBiRefreshScheduler
{
    /// <summary>Marca un refresh pendiente (cierre de turno) e intenta lanzarlo si la política lo permite.</summary>
    Task SolicitarAsync(string motivo, CancellationToken ct);

    /// <summary>Lanza el refresh pendiente si ya se cumplen la ventana, el intervalo y el máximo diario.</summary>
    Task IntentarPendienteAsync(CancellationToken ct);
}

/// <summary>
/// Username de la identidad efectiva: códigos de planta separados por "|" (p. ej. "LEO|SIL").
/// En el dataset, el rol RLS filtra con <c>PATHCONTAINS(USERNAME(), dim_planta[codigo])</c>,
/// así no hace falta una tabla de seguridad por usuario.
/// </summary>
public static class IdentidadRls
{
    public const char Separador = '|';

    public static string Crear(IEnumerable<string> codigosPlanta)
    {
        var codigos = codigosPlanta
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (codigos.Length == 0)
            throw new ForbiddenAccessException("No tienes plantas asignadas para ver reportes.");
        if (codigos.Any(c => c.Contains(Separador)))
            throw new InvalidOperationException($"Un código de planta no puede contener '{Separador}'.");

        return string.Join(Separador, codigos);
    }
}

/// <summary>Estado persistido del refresh (una sola fila en la base).</summary>
public sealed record EstadoRefresh(
    bool Pendiente,
    DateTimeOffset? SolicitadoEnUtc,
    DateTimeOffset? UltimoRefreshUtc,
    DateOnly? FechaConteo,
    int ConteoDelDia);

/// <summary>
/// Cuándo se puede refrescar el dataset. Agrupa los cierres que llegan juntos (la primera solicitud pendiente
/// debe tener al menos la ventana de agrupación), respeta un intervalo mínimo entre refreshes y un máximo
/// diario en UTC (Power BI Pro permite 8 al día). Clase pura para poder probarla.
/// </summary>
public sealed class PoliticaRefresh(TimeSpan ventanaAgrupacion, TimeSpan intervaloMinimo, int maximoPorDia)
{
    public TimeSpan VentanaAgrupacion { get; } = ventanaAgrupacion;
    public TimeSpan IntervaloMinimo { get; } = intervaloMinimo;
    public int MaximoPorDia { get; } = maximoPorDia;

    public static int RefreshesDelDia(EstadoRefresh estado, DateTimeOffset ahoraUtc) =>
        estado.FechaConteo == DateOnly.FromDateTime(ahoraUtc.UtcDateTime) ? estado.ConteoDelDia : 0;

    public bool PuedeRefrescar(EstadoRefresh estado, DateTimeOffset ahoraUtc)
    {
        if (!estado.Pendiente || estado.SolicitadoEnUtc is not { } solicitado) return false;
        if (ahoraUtc - solicitado < VentanaAgrupacion) return false;
        if (estado.UltimoRefreshUtc is { } ultimo && ahoraUtc - ultimo < IntervaloMinimo) return false;
        return RefreshesDelDia(estado, ahoraUtc) < MaximoPorDia;
    }
}

// ───────────── Queries ─────────────

/// <summary>Forma acordada con el frontend: { reportId, embedUrl, accessToken, expiresAtUtc }.</summary>
public sealed record EmbedPowerBiDto(Guid ReportId, string EmbedUrl, string AccessToken, DateTimeOffset ExpiresAtUtc);

/// <summary>Embed token para un reporte. Con PlantaId restringe el RLS a esa planta (validando acceso).</summary>
public sealed record ObtenerEmbedPowerBiQuery(string ReportKey, Guid? PlantaId) : IRequest<EmbedPowerBiDto>;

public sealed class ObtenerEmbedPowerBiValidator : AbstractValidator<ObtenerEmbedPowerBiQuery>
{
    public ObtenerEmbedPowerBiValidator() =>
        RuleFor(x => x.ReportKey).NotEmpty().WithMessage("La clave del reporte es obligatoria.").MaximumLength(50);
}

public sealed class ObtenerEmbedPowerBiHandler(
    IPowerBiEmbedService powerBi, IPlantaRepository plantas, ICurrentUser usuario, IPowerBiRefreshScheduler refresh)
    : IRequestHandler<ObtenerEmbedPowerBiQuery, EmbedPowerBiDto>
{
    public async Task<EmbedPowerBiDto> Handle(ObtenerEmbedPowerBiQuery request, CancellationToken ct)
    {
        powerBi.Validar(request.ReportKey);
        var visibles = await plantas.ListarAsync(usuario.PlantasVisibles(request.PlantaId), ct);
        var username = IdentidadRls.Crear(visibles.Select(p => p.Codigo));

        // Abrir un reporte es el otro momento en que se atiende un refresh pendiente (en Lambda no hay
        // proceso en segundo plano). Nunca lanza: si falla, el reporte se muestra con los datos anteriores.
        await refresh.IntentarPendienteAsync(ct);

        var embed = await powerBi.GenerarAsync(request.ReportKey, username, ct);
        return new EmbedPowerBiDto(embed.ReportId, embed.EmbedUrl, embed.AccessToken, embed.ExpiresAtUtc);
    }
}

public sealed record ListarReportesPowerBiQuery : IRequest<IReadOnlyList<ReportePowerBi>>;

public sealed class ListarReportesPowerBiHandler(IPowerBiEmbedService powerBi)
    : IRequestHandler<ListarReportesPowerBiQuery, IReadOnlyList<ReportePowerBi>>
{
    public Task<IReadOnlyList<ReportePowerBi>> Handle(ListarReportesPowerBiQuery request, CancellationToken ct) =>
        Task.FromResult(powerBi.Reportes());
}
