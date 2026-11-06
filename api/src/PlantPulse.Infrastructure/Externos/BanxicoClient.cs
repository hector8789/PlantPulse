using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PlantPulse.Application.Common;
using PlantPulse.Domain.TiposCambio;

namespace PlantPulse.Infrastructure.Externos;

/// <summary>
/// API SIE de Banxico: <c>series/SF43718/datos/{inicio}/{fin}</c> con el header <c>Bmx-Token</c>.
/// Respuesta: { "bmx": { "series": [ { "idSerie": "SF43718", "datos": [ { "fecha": "dd/MM/yyyy", "dato": "18.1234" } ] } ] } }.
/// Los días sin publicación pueden venir como "N/E"; se descartan.
/// </summary>
internal sealed class BanxicoClient(HttpClient http, BanxicoSettings settings, ILogger<BanxicoClient> logger) : ITipoCambioProvider
{
    public const string Servicio = "Banxico";
    public const string HeaderToken = "Bmx-Token";

    public async Task<IReadOnlyList<TipoCambioPublicado>> ObtenerFixAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.Token))
            throw new ExternalServiceException(Servicio,
                "Falta el token de Banxico. Configúralo con: dotnet user-secrets set \"Banxico:Token\" \"...\" --project src/PlantPulse.Api");

        var ruta = $"series/{TipoCambio.SerieFix}/datos/{desde:yyyy-MM-dd}/{hasta:yyyy-MM-dd}";
        using var mensaje = new HttpRequestMessage(HttpMethod.Get, ruta);
        mensaje.Headers.Add(HeaderToken, settings.Token);
        mensaje.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var respuesta = await http.SendAsync(mensaje, ct);
            respuesta.EnsureSuccessStatusCode();
            var cuerpo = await respuesta.Content.ReadFromJsonAsync<BanxicoRespuesta>(ct);
            return Convertir(cuerpo);
        }
        catch (Exception ex) when (ErpODataClient.EsFallaDeRed(ex, ct))
        {
            logger.LogError(ex, "Banxico no respondió después de los reintentos.");
            throw new ExternalServiceException(Servicio, "Banxico no está disponible en este momento.", ex);
        }
    }

    internal static IReadOnlyList<TipoCambioPublicado> Convertir(BanxicoRespuesta? cuerpo)
    {
        var datos = cuerpo?.Bmx?.Series?.FirstOrDefault(s => s.IdSerie == TipoCambio.SerieFix)?.Datos ?? [];
        var lista = new List<TipoCambioPublicado>();
        foreach (var d in datos)
        {
            if (!DateOnly.TryParseExact(d.Fecha, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)) continue;
            if (!decimal.TryParse(d.Dato?.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor)) continue;
            if (valor <= 0m) continue;
            lista.Add(new TipoCambioPublicado(fecha, valor));
        }
        return lista.OrderBy(t => t.Fecha).ToList();
    }

    internal sealed record BanxicoRespuesta([property: JsonPropertyName("bmx")] BanxicoBmx? Bmx);
    internal sealed record BanxicoBmx([property: JsonPropertyName("series")] List<BanxicoSerie>? Series);
    internal sealed record BanxicoSerie(
        [property: JsonPropertyName("idSerie")] string IdSerie,
        [property: JsonPropertyName("datos")] List<BanxicoDato>? Datos);
    internal sealed record BanxicoDato(
        [property: JsonPropertyName("fecha")] string Fecha,
        [property: JsonPropertyName("dato")] string? Dato);
}

public sealed class BanxicoSettings
{
    public const string SectionName = "Banxico";
    public string BaseUrl { get; set; } = "https://www.banxico.org.mx/SieAPIRest/service/v1/";
    public string? Token { get; set; }
}

public sealed class ErpSettings
{
    public const string SectionName = "Erp";

    /// <summary>En desarrollo apunta al ERP simulado de la misma API: https://localhost:{puerto}/erp/odata/</summary>
    public string? BaseUrl { get; set; }
}
