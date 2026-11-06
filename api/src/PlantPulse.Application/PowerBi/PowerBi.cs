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

/// <summary>Cola de refresh del dataset. No bloquea: el refresh real se hace en segundo plano con debounce.</summary>
public interface IPowerBiRefreshScheduler
{
    void Solicitar(string motivo);
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

/// <summary>
/// Cuándo se puede refrescar el dataset. Agrupa los cierres que llegan juntos (fin de turno de varias líneas),
/// respeta un intervalo mínimo entre refreshes y un máximo diario (Power BI Pro permite 8 al día;
/// Premium/Fabric/Embedded, 48 programados o más por API). Clase pura para poder probarla.
/// </summary>
public sealed class PoliticaRefresh(TimeSpan ventanaAgrupacion, TimeSpan intervaloMinimo, int maximoPorDia)
{
    private readonly List<DateTimeOffset> _refreshes = [];

    public TimeSpan VentanaAgrupacion { get; } = ventanaAgrupacion;
    public TimeSpan IntervaloMinimo { get; } = intervaloMinimo;
    public int MaximoPorDia { get; } = maximoPorDia;

    public DateTimeOffset? UltimoRefresh => _refreshes.Count == 0 ? null : _refreshes[^1];

    public int RefreshesDelDia(DateTimeOffset ahoraUtc) =>
        _refreshes.Count(r => r.UtcDateTime.Date == ahoraUtc.UtcDateTime.Date);

    /// <summary>Cuánto esperar, desde ahora, antes de lanzar el siguiente refresh.</summary>
    public TimeSpan CalcularEspera(DateTimeOffset ahoraUtc)
    {
        if (RefreshesDelDia(ahoraUtc) >= MaximoPorDia)
        {
            var manana = new DateTimeOffset(ahoraUtc.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
            return manana - ahoraUtc + VentanaAgrupacion;
        }

        var porIntervalo = UltimoRefresh is { } ultimo ? ultimo + IntervaloMinimo - ahoraUtc : TimeSpan.Zero;
        return porIntervalo > VentanaAgrupacion ? porIntervalo : VentanaAgrupacion;
    }

    public void Registrar(DateTimeOffset ahoraUtc)
    {
        _refreshes.Add(ahoraUtc);
        _refreshes.RemoveAll(r => r < ahoraUtc.AddDays(-2));
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

public sealed class ObtenerEmbedPowerBiHandler(IPowerBiEmbedService powerBi, IPlantaRepository plantas, ICurrentUser usuario)
    : IRequestHandler<ObtenerEmbedPowerBiQuery, EmbedPowerBiDto>
{
    public async Task<EmbedPowerBiDto> Handle(ObtenerEmbedPowerBiQuery request, CancellationToken ct)
    {
        powerBi.Validar(request.ReportKey);
        var visibles = await plantas.ListarAsync(usuario.PlantasVisibles(request.PlantaId), ct);
        var username = IdentidadRls.Crear(visibles.Select(p => p.Codigo));

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
