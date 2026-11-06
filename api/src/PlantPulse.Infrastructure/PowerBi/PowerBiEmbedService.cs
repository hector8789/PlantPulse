using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using PlantPulse.Application.Common;
using PlantPulse.Application.PowerBi;

namespace PlantPulse.Infrastructure.PowerBi;

/// <summary>Token de Azure AD del service principal (MSAL guarda en caché el token hasta que expira).</summary>
internal sealed class PowerBiAuth(PowerBiSettings settings)
{
    private const string Scope = "https://analysis.windows.net/powerbi/api/.default";

    private readonly Lazy<IConfidentialClientApplication> _app = new(() =>
        ConfidentialClientApplicationBuilder.Create(settings.ClientId)
            .WithClientSecret(settings.ClientSecret)
            .WithAuthority($"{settings.AuthorityHost.TrimEnd('/')}/{settings.TenantId}")
            .Build());

    public async Task<string> ObtenerTokenAsync(CancellationToken ct)
    {
        var resultado = await _app.Value.AcquireTokenForClient([Scope]).ExecuteAsync(ct);
        return resultado.AccessToken;
    }
}

internal interface IPowerBiDatasetRefresher
{
    Task RefrescarAsync(CancellationToken ct);
}

/// <summary>
/// Embed tokens (App Owns Data) con Microsoft.PowerBI.Api 4.22 y MSAL:
/// 1) MSAL obtiene el token de Azure AD del service principal;
/// 2) se lee el reporte (embedUrl y dataset);
/// 3) se genera el embed token V2 con la identidad efectiva del RLS (username = "LEO|SIL", rol PorPlanta).
/// </summary>
internal sealed class PowerBiEmbedService(PowerBiSettings settings, PowerBiAuth auth, ILogger<PowerBiEmbedService> logger)
    : IPowerBiEmbedService, IPowerBiDatasetRefresher
{
    private sealed record InfoReporte(string EmbedUrl, string DatasetId);

    private readonly ConcurrentDictionary<Guid, InfoReporte> _reportes = new();

    public IReadOnlyList<ReportePowerBi> Reportes() =>
        settings.Reportes.Select(r => new ReportePowerBi(r.Key, r.Nombre)).ToList();

    public void Validar(string reportKey) => BuscarReporte(reportKey);

    private ReporteConfig BuscarReporte(string reportKey)
    {
        settings.AsegurarConfigurado();
        var reporte = settings.Reportes.FirstOrDefault(r => string.Equals(r.Key, reportKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("Reporte de Power BI", reportKey);
        if (reporte.ReportId == Guid.Empty)
            throw new ExternalServiceException(PowerBiSettings.Servicio, $"El reporte '{reporte.Key}' no tiene PowerBi:Reportes:{reporte.Key}:ReportId configurado.");
        return reporte;
    }

    public async Task<EmbedPowerBi> GenerarAsync(string reportKey, string usernameRls, CancellationToken ct)
    {
        var reporte = BuscarReporte(reportKey);

        return await Ejecutar(async client =>
        {
            var info = await ObtenerReporteAsync(client, reporte.ReportId, ct);

            var request = new GenerateTokenRequestV2(
                datasets: [new GenerateTokenRequestV2Dataset(info.DatasetId)],
                reports: [new GenerateTokenRequestV2Report(reporte.ReportId)],
                targetWorkspaces: [new GenerateTokenRequestV2TargetWorkspace(settings.WorkspaceId)],
                identities: settings.UsarRls
                    ? [new EffectiveIdentity(username: usernameRls, datasets: [info.DatasetId], roles: [settings.RolRls])]
                    : null);

            var token = await client.EmbedToken.GenerateTokenAsync(request, ct);
            var expira = new DateTimeOffset(DateTime.SpecifyKind(token.Expiration, DateTimeKind.Utc));

            logger.LogInformation("Embed token para {Reporte} (RLS {Username}) válido hasta {Expira:u}.", reporte.Key, usernameRls, expira);
            return new EmbedPowerBi(reporte.ReportId, info.EmbedUrl, token.Token, expira);
        }, ct);
    }

    /// <summary>Lanza el refresh de cada dataset (el configurado o los de los reportes).</summary>
    public async Task RefrescarAsync(CancellationToken ct)
    {
        settings.AsegurarConfigurado();

        await Ejecutar(async client =>
        {
            var datasets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (settings.DatasetId is { } fijo) datasets.Add(fijo);
            else
                foreach (var r in settings.Reportes.Where(r => r.ReportId != Guid.Empty))
                    datasets.Add((await ObtenerReporteAsync(client, r.ReportId, ct)).DatasetId);

            foreach (var datasetId in datasets)
            {
                try
                {
                    await client.Datasets.RefreshDatasetInGroupAsync(
                        settings.WorkspaceId, datasetId, new DatasetRefreshRequest { NotifyOption = NotifyOption.NoNotification }, ct);
                    logger.LogInformation("Refresh solicitado para el dataset {DatasetId}.", datasetId);
                }
                catch (HttpOperationException ex) when ((int?)ex.Response?.StatusCode is 400 or 409
                                                        && Detalle(ex).Contains("refresh", StringComparison.OrdinalIgnoreCase))
                {
                    // Ya hay un refresh en curso: el que corre incluirá los datos nuevos o los tomará el siguiente.
                    logger.LogWarning("El dataset {DatasetId} ya tiene un refresh en curso: {Detalle}", datasetId, Detalle(ex));
                }
            }
            return true;
        }, ct);
    }

    private async Task<InfoReporte> ObtenerReporteAsync(PowerBIClient client, Guid reportId, CancellationToken ct)
    {
        if (_reportes.TryGetValue(reportId, out var info)) return info;
        var r = await client.Reports.GetReportInGroupAsync(settings.WorkspaceId, reportId, ct);
        info = new InfoReporte(r.EmbedUrl, r.DatasetId);
        _reportes[reportId] = info;
        return info;
    }

    private async Task<T> Ejecutar<T>(Func<PowerBIClient, Task<T>> accion, CancellationToken ct)
    {
        try
        {
            var tokenAad = await auth.ObtenerTokenAsync(ct);
            using var client = new PowerBIClient(new Uri(settings.ApiUrl), new TokenCredentials(tokenAad, "Bearer"));
            return await accion(client);
        }
        catch (MsalException ex)
        {
            logger.LogError(ex, "MSAL no pudo obtener el token del service principal.");
            throw new ExternalServiceException(PowerBiSettings.Servicio,
                "No se pudo autenticar el service principal en Azure AD. Revisa TenantId, ClientId y ClientSecret.", ex);
        }
        catch (HttpOperationException ex)
        {
            var status = (int?)ex.Response?.StatusCode;
            logger.LogError(ex, "Power BI respondió {Status}: {Detalle}", status, Detalle(ex));
            throw new ExternalServiceException(PowerBiSettings.Servicio, status switch
            {
                401 or 403 => "El service principal no tiene acceso al workspace de Power BI (agrégalo como Miembro o Admin).",
                404 => "El workspace o el reporte no existe en Power BI. Revisa WorkspaceId y ReportId.",
                _ => $"Power BI respondió {status}: {Detalle(ex)}",
            }, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException(PowerBiSettings.Servicio, "No se pudo conectar con Power BI.", ex);
        }
    }

    private static string Detalle(HttpOperationException ex)
    {
        var cuerpo = ex.Response?.Content;
        return string.IsNullOrWhiteSpace(cuerpo) ? ex.Message : cuerpo.Length > 500 ? cuerpo[..500] : cuerpo;
    }
}
