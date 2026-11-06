using Microsoft.Extensions.Configuration;
using PlantPulse.Application.Common;

namespace PlantPulse.Infrastructure.PowerBi;

public sealed record ReporteConfig(string Key, string Nombre, Guid ReportId);

/// <summary>
/// Sección "PowerBi". TenantId, ClientId, ClientSecret y WorkspaceId van en user-secrets;
/// los reportes y la política de refresh van en appsettings.
/// </summary>
public sealed class PowerBiSettings
{
    public const string SectionName = "PowerBi";
    public const string Servicio = "Power BI";

    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public Guid WorkspaceId { get; init; }

    /// <summary>Si se indica, es el dataset que se refresca; si no, se usan los datasets de los reportes.</summary>
    public string? DatasetId { get; init; }

    /// <summary>Rol RLS definido en el dataset. Su filtro: PATHCONTAINS(USERNAME(), dim_planta[codigo]).</summary>
    public string RolRls { get; init; } = "PorPlanta";

    /// <summary>Desactívalo solo mientras el dataset todavía no tiene el rol RLS creado.</summary>
    public bool UsarRls { get; init; } = true;

    public string ApiUrl { get; init; } = "https://api.powerbi.com";
    public string AuthorityHost { get; init; } = "https://login.microsoftonline.com";
    public IReadOnlyList<ReporteConfig> Reportes { get; init; } = [];

    public bool RefreshHabilitado { get; init; } = true;
    public int VentanaAgrupacionSegundos { get; init; } = 60;
    public int IntervaloMinimoMinutos { get; init; } = 30;
    public int MaximoRefreshPorDia { get; init; } = 8;

    public bool EstaConfigurado => FaltantesDeConfiguracion().Count == 0;

    public IReadOnlyList<string> FaltantesDeConfiguracion()
    {
        var faltan = new List<string>();
        if (string.IsNullOrWhiteSpace(TenantId)) faltan.Add("PowerBi:TenantId");
        if (string.IsNullOrWhiteSpace(ClientId)) faltan.Add("PowerBi:ClientId");
        if (string.IsNullOrWhiteSpace(ClientSecret)) faltan.Add("PowerBi:ClientSecret");
        if (WorkspaceId == Guid.Empty) faltan.Add("PowerBi:WorkspaceId");
        return faltan;
    }

    /// <summary>503 con la lista de lo que falta, para que sea obvio qué configurar.</summary>
    public void AsegurarConfigurado()
    {
        var faltan = FaltantesDeConfiguracion();
        if (faltan.Count > 0)
            throw new ExternalServiceException(Servicio,
                $"Power BI no está configurado. Faltan en user-secrets: {string.Join(", ", faltan)}.");
    }

    /// <summary>Lectura manual de la configuración (solo Configuration.Abstractions, sin binder).</summary>
    public static PowerBiSettings Desde(IConfiguration configuration)
    {
        var s = configuration.GetSection(SectionName);
        var refresh = s.GetSection("Refresh");

        return new PowerBiSettings
        {
            TenantId = s["TenantId"],
            ClientId = s["ClientId"],
            ClientSecret = s["ClientSecret"],
            WorkspaceId = Guid.TryParse(s["WorkspaceId"], out var ws) ? ws : Guid.Empty,
            DatasetId = string.IsNullOrWhiteSpace(s["DatasetId"]) ? null : s["DatasetId"],
            RolRls = string.IsNullOrWhiteSpace(s["RolRls"]) ? "PorPlanta" : s["RolRls"]!,
            UsarRls = !bool.TryParse(s["UsarRls"], out var rls) || rls,
            ApiUrl = string.IsNullOrWhiteSpace(s["ApiUrl"]) ? "https://api.powerbi.com" : s["ApiUrl"]!,
            AuthorityHost = string.IsNullOrWhiteSpace(s["AuthorityHost"]) ? "https://login.microsoftonline.com" : s["AuthorityHost"]!,
            Reportes = s.GetSection("Reportes").GetChildren()
                .Select(r => new ReporteConfig(
                    r.Key.ToLowerInvariant(),
                    string.IsNullOrWhiteSpace(r["Nombre"]) ? r.Key : r["Nombre"]!,
                    Guid.TryParse(r["ReportId"], out var id) ? id : Guid.Empty))
                .ToList(),
            RefreshHabilitado = !bool.TryParse(refresh["Habilitado"], out var h) || h,
            VentanaAgrupacionSegundos = Entero(refresh["VentanaAgrupacionSegundos"], 60),
            IntervaloMinimoMinutos = Entero(refresh["IntervaloMinimoMinutos"], 30),
            MaximoRefreshPorDia = Entero(refresh["MaximoPorDia"], 8),
        };
    }

    private static int Entero(string? valor, int porDefecto) =>
        int.TryParse(valor, out var n) && n >= 0 ? n : porDefecto;
}
