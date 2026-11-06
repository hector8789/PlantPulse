using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PlantPulse.Api.ErpSimulado;

/// <summary>
/// ERP simulado con forma de servicio OData V4 (como uno de SAP). Vive en la misma API solo para la demo;
/// PlantPulse lo consume como si fuera externo, por HTTP y con Polly.
/// <para>Soporta <c>$filter=Plant eq 'LEO'</c>, <c>$top</c>, <c>$skip</c> y pagina con <c>@odata.nextLink</c>.</para>
/// <para>Para probar la resiliencia: <c>ErpSimulado:TasaFallas</c> (0 a 1) responde 503 al azar y
/// <c>ErpSimulado:LatenciaMs</c> agrega retraso.</para>
/// </summary>
[ApiController]
[Route("erp/odata")]
[AllowAnonymous]
public sealed partial class ErpSimuladoController(IConfiguration configuration, TimeProvider reloj) : ControllerBase
{
    public const int TamanoPagina = 20;

    private static readonly JsonSerializerOptions JsonOData = new() { PropertyNamingPolicy = null };

    [GeneratedRegex(@"^\s*Plant\s+eq\s+'(?<planta>[^']*)'\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex FiltroPlanta();

    /// <summary>Órdenes de producción (entity set ProductionOrders).</summary>
    [HttpGet("ProductionOrders")]
    public async Task<IActionResult> ProductionOrders(
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        CancellationToken ct)
    {
        var latencia = configuration.GetValue("ErpSimulado:LatenciaMs", 0);
        if (latencia > 0) await Task.Delay(latencia, ct);

        var tasaFallas = configuration.GetValue("ErpSimulado:TasaFallas", 0d);
        if (tasaFallas > 0 && Random.Shared.NextDouble() < tasaFallas)
            return ErrorOData(StatusCodes.Status503ServiceUnavailable, "ServiceUnavailable", "ERP simulado: falla inyectada.");

        string? planta = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var m = FiltroPlanta().Match(filter);
            if (!m.Success)
                return ErrorOData(StatusCodes.Status400BadRequest, "BadRequest", "Solo se soporta $filter=Plant eq '<código>'.");
            planta = m.Groups["planta"].Value;
        }

        var todas = DatosErpSimulado.Ordenes(DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime))
            .Where(o => planta is null || string.Equals(o.Plant, planta, StringComparison.OrdinalIgnoreCase))
            .OrderBy(o => o.OrderNumber, StringComparer.Ordinal)
            .ToList();

        var desde = Math.Max(skip ?? 0, 0);
        var tamano = Math.Clamp(top ?? TamanoPagina, 1, TamanoPagina);
        var pagina = todas.Skip(desde).Take(tamano).ToList();

        var cuerpo = new Dictionary<string, object?>
        {
            ["@odata.context"] = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/erp/odata/$metadata#ProductionOrders",
            ["value"] = pagina,
        };

        if (desde + pagina.Count < todas.Count)
        {
            var query = new List<string>();
            if (filter is not null) query.Add($"$filter={Uri.EscapeDataString(filter)}");
            query.Add($"$skip={desde + pagina.Count}");
            cuerpo["@odata.nextLink"] =
                $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}?{string.Join('&', query)}";
        }

        return new JsonResult(cuerpo, JsonOData);
    }

    private static JsonResult ErrorOData(int status, string codigo, string mensaje) =>
        new(new { error = new { code = codigo, message = mensaje } }, JsonOData) { StatusCode = status };
}

/// <summary>Orden con nombres de propiedad estilo SAP/OData.</summary>
public sealed record OrdenErpSimulada(
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

/// <summary>
/// Datos deterministas: dos órdenes por línea a partir del lunes siguiente, más dos casos que
/// PlantPulse debe omitir (centro de trabajo inexistente y orden cerrada técnicamente).
/// Repetir la sincronización el mismo día no crea nada nuevo.
/// </summary>
public static class DatosErpSimulado
{
    private sealed record Linea(string Planta, string Centro, string Material, string Descripcion, decimal? Ciclo, decimal Costo, int PiezasDia);

    private static readonly Linea[] Lineas =
    [
        new("LEO", "LEO-L01", "CAR-4521", "Carcasa de transmisión", null, 412.50m, 1350),
        new("LEO", "LEO-L02", "TAP-3307", "Tapa de cadena de distribución", null, 238.40m, 1220),
        new("LEO", "LEO-L03", "BRZ-2210", "Brazo de suspensión", 90m, 524.75m, 730),
        new("SIL", "SIL-L01", "ARN-7710", "Arnés de puerta", 26m, 142.60m, 2500),
        new("SIL", "SIL-L02", "REJ-5530", "Rejilla de ventilación", 44m, 58.90m, 1530),
    ];

    public static IReadOnlyList<OrdenErpSimulada> Ordenes(DateOnly hoy)
    {
        var diasHastaLunes = ((int)DayOfWeek.Monday - (int)hoy.DayOfWeek + 7) % 7;
        var lunes = hoy.AddDays(diasHastaLunes == 0 ? 7 : diasHastaLunes);
        var lista = new List<OrdenErpSimulada>();

        for (var i = 0; i < Lineas.Length; i++)
        {
            var l = Lineas[i];
            var inicio = lunes;
            for (var k = 0; k < 2; k++)
            {
                var dias = 3 + (i + k) % 2;   // 3 o 4 días
                var fin = inicio.AddDays(dias - 1);
                lista.Add(new OrdenErpSimulada(
                    $"4{i}{lunes:yyMMdd}{k}", l.Planta, l.Centro, l.Material, l.Descripcion,
                    l.PiezasDia * dias, Fecha(inicio), Fecha(fin), l.Ciclo, l.Costo, "REL"));
                inicio = fin.AddDays(1);
            }
        }

        lista.Add(new OrdenErpSimulada($"49{lunes:yyMMdd}0", "LEO", "LEO-L09", "CAR-4521", "Carcasa de transmisión",
            900, Fecha(lunes), Fecha(lunes.AddDays(1)), null, 412.50m, "REL"));
        lista.Add(new OrdenErpSimulada($"49{lunes:yyMMdd}1", "SIL", "SIL-L01", "ARN-7702", "Arnés de tablero",
            1200, Fecha(lunes.AddDays(-14)), Fecha(lunes.AddDays(-12)), null, 318.20m, "TECO"));

        return lista;
    }

    private static string Fecha(DateOnly d) => d.ToString("yyyy-MM-dd");
}
