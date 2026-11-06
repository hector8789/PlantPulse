using System.Net;
using System.Text.Json;
using PlantPulse.Api.ErpSimulado;
using Xunit;

namespace PlantPulse.Api.IntegrationTests;

public class ErpSimuladoTests(PlantPulseApiFactory factory) : IClassFixture<PlantPulseApiFactory>
{
    private async Task<(HttpStatusCode Status, JsonElement Json)> Get(string url)
    {
        var r = await factory.CreateClient().GetAsync(url);
        return (r.StatusCode, JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement);
    }

    [Fact]
    public async Task Filtra_por_planta_con_forma_OData()
    {
        var (status, json) = await Get("/erp/odata/ProductionOrders?$filter=Plant eq 'LEO'");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.EndsWith("$metadata#ProductionOrders", json.GetProperty("@odata.context").GetString());
        var ordenes = json.GetProperty("value").EnumerateArray().ToList();
        Assert.NotEmpty(ordenes);
        Assert.All(ordenes, o => Assert.Equal("LEO", o.GetProperty("Plant").GetString()));
        Assert.True(ordenes[0].TryGetProperty("OrderNumber", out _));   // nombres estilo SAP (PascalCase)
    }

    [Fact]
    public async Task Pagina_con_nextLink()
    {
        var (_, json) = await Get("/erp/odata/ProductionOrders?$top=5");

        Assert.Equal(5, json.GetProperty("value").GetArrayLength());
        Assert.Contains("$skip=5", json.GetProperty("@odata.nextLink").GetString());
    }

    [Fact]
    public async Task Ultima_pagina_no_trae_nextLink()
    {
        var total = DatosErpSimulado.Ordenes(DateOnly.FromDateTime(DateTime.UtcNow)).Count;
        var (_, json) = await Get($"/erp/odata/ProductionOrders?$skip={total - 1}");

        Assert.Equal(1, json.GetProperty("value").GetArrayLength());
        Assert.False(json.TryGetProperty("@odata.nextLink", out _));
    }

    [Fact]
    public async Task Filtro_no_soportado_responde_400_con_error_OData()
    {
        var (status, json) = await Get("/erp/odata/ProductionOrders?$filter=Material eq 'X'");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("BadRequest", json.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void Datos_incluyen_los_casos_que_PlantPulse_debe_omitir()
    {
        var ordenes = DatosErpSimulado.Ordenes(new DateOnly(2026, 11, 4));
        Assert.Contains(ordenes, o => o.WorkCenter == "LEO-L09");
        Assert.Contains(ordenes, o => o.Status == "TECO");
        Assert.Equal(ordenes.Count, ordenes.Select(o => o.OrderNumber).Distinct().Count());
        // Las órdenes liberadas de cada línea arrancan el lunes siguiente (la TECO es histórica y se excluye).
        var liberadas = ordenes.Where(o => o.Status == "REL" && o.WorkCenter != "LEO-L09").ToList();
        Assert.All(liberadas,
            o => Assert.Equal("2026-11-09", liberadas.Where(x => x.WorkCenter == o.WorkCenter).Min(x => x.PlannedStart)));
    }
}
