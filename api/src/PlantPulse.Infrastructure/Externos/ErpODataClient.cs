using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Polly;
using PlantPulse.Application.Common;

namespace PlantPulse.Infrastructure.Externos;

/// <summary>
/// Cliente OData V4 del ERP (simulado en la API, con la forma de un servicio de SAP).
/// Consulta <c>ProductionOrders?$filter=Plant eq 'LEO'</c> y sigue <c>@odata.nextLink</c> para paginar.
/// La resiliencia (reintentos, timeout, circuit breaker) la agrega el HttpClient registrado en DI.
/// </summary>
internal sealed class ErpODataClient(HttpClient http, ILogger<ErpODataClient> logger) : IErpOrdenesClient
{
    public const string Servicio = "ERP";
    private const int MaximoPaginas = 50;

    public async Task<IReadOnlyList<OrdenErp>> ObtenerOrdenesAsync(string plantaCodigo, CancellationToken ct)
    {
        var filtro = Uri.EscapeDataString($"Plant eq '{plantaCodigo.Replace("'", "''")}'");
        string? url = $"ProductionOrders?$filter={filtro}&$orderby=OrderNumber";
        var resultado = new List<OrdenErp>();

        try
        {
            for (var pagina = 0; url is not null; pagina++)
            {
                if (pagina >= MaximoPaginas)
                    throw new ExternalServiceException(Servicio, $"El ERP devolvió más de {MaximoPaginas} páginas; se detuvo la lectura.");

                var respuesta = await http.GetFromJsonAsync<ODataPagina>(url, Json, ct)
                    ?? throw new ExternalServiceException(Servicio, "El ERP devolvió una respuesta vacía.");

                resultado.AddRange(respuesta.Value.Select(Mapear));
                url = respuesta.NextLink;
            }
        }
        catch (Exception ex) when (EsFallaDeRed(ex, ct))
        {
            logger.LogError(ex, "El ERP no respondió después de los reintentos.");
            throw new ExternalServiceException(Servicio, "El ERP no está disponible en este momento. Intenta más tarde.", ex);
        }

        return resultado;
    }

    internal static bool EsFallaDeRed(Exception ex, CancellationToken ct) => ex switch
    {
        HttpRequestException => true,
        ExecutionRejectedException => true,           // circuito abierto o timeout de Polly
        TaskCanceledException when !ct.IsCancellationRequested => true,   // timeout de HttpClient
        JsonException => true,
        _ => false,
    };

    private static OrdenErp Mapear(ODataOrden o) => new(
        o.OrderNumber, o.Plant, o.WorkCenter, o.Material, o.MaterialDescription, o.PlannedQuantity,
        DateOnly.ParseExact(o.PlannedStart, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateOnly.ParseExact(o.PlannedEnd, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        o.IdealCycleTimeSec, o.StandardCostMxn, o.Status);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record ODataPagina(
        [property: JsonPropertyName("value")] List<ODataOrden> Value,
        [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

    private sealed record ODataOrden(
        string OrderNumber,
        string Plant,
        string WorkCenter,
        string Material,
        string MaterialDescription,
        int PlannedQuantity,
        string PlannedStart,
        string PlannedEnd,
        decimal? IdealCycleTimeSec,
        decimal? StandardCostMxn,
        string Status);
}
