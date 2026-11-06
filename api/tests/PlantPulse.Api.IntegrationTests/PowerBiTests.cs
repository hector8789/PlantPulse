using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace PlantPulse.Api.IntegrationTests;

/// <summary>Sin credenciales de Azure en las pruebas: se valida el contrato (401, 503, lista de reportes) sin red ni base.</summary>
public class PowerBiTests(PlantPulseApiFactory factory) : IClassFixture<PlantPulseApiFactory>
{
    private HttpClient Cliente(string? token)
    {
        var c = factory.CreateClient();
        if (token is not null) c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task Embed_sin_token_es_401()
    {
        var r = await Cliente(null).GetAsync("/api/powerbi/embed/oee");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Embed_sin_configuracion_de_Azure_es_503_y_dice_que_falta()
    {
        var token = PlantPulseApiFactory.Token(["Admin"], []);
        var r = await Cliente(token).GetAsync("/api/powerbi/embed/oee");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Power BI", json.GetProperty("servicio").GetString());
        Assert.Contains("PowerBi:TenantId", json.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Lista_los_reportes_configurados()
    {
        var token = PlantPulseApiFactory.Token(["Consulta"], [Guid.NewGuid()]);
        var json = JsonDocument.Parse(await (await Cliente(token).GetAsync("/api/powerbi/reportes")).Content.ReadAsStringAsync()).RootElement;

        var claves = json.EnumerateArray().Select(e => e.GetProperty("key").GetString()).ToList();
        Assert.Contains("oee", claves);
        Assert.Contains("pareto-paros", claves);
    }
}
