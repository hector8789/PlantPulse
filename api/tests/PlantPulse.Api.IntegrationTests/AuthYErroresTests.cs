using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PlantPulse.Api.IntegrationTests;

/// <summary>Contrato de errores (401, 403, 422) sin base de datos: todos fallan antes de llegar al repositorio.</summary>
public class AuthYErroresTests(PlantPulseApiFactory factory) : IClassFixture<PlantPulseApiFactory>
{
    private static readonly Guid PlantaLeon = Guid.Parse("0195f3a0-0000-7000-8000-000000000001");

    private HttpClient Cliente(string? token = null)
    {
        var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Sin_token_responde_401_con_ProblemDetails()
    {
        var r = await Cliente().GetAsync("/api/catalogos/plantas");

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        Assert.Equal(401, (await Json(r)).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Token_firmado_con_otra_llave_responde_401()
    {
        var token = PlantPulseApiFactory.Token(["Admin"], [], llave: "otra-llave-que-no-es-la-del-servidor-de-pruebas!!");
        var r = await Cliente(token).GetAsync("/api/catalogos/plantas");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Rol_consulta_no_puede_abrir_turnos_403()
    {
        var token = PlantPulseApiFactory.Token(["Consulta"], [PlantaLeon]);
        var r = await Cliente(token).PostAsJsonAsync("/api/turnos-linea", new { lineaId = Guid.NewGuid(), turnoId = Guid.NewGuid(), fechaOperativa = "2026-11-04" });

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Supervisor_no_puede_reabrir_turnos_403()
    {
        var token = PlantPulseApiFactory.Token(["Supervisor"], [PlantaLeon]);
        var r = await Cliente(token).PostAsync($"/api/turnos-linea/{Guid.NewGuid()}/reabrir", null);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Login_vacio_responde_422_con_errores_por_campo_en_camelCase()
    {
        var r = await Cliente().PostAsJsonAsync("/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        var errores = (await Json(r)).GetProperty("errors");
        Assert.True(errores.TryGetProperty("email", out _));
        Assert.True(errores.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Json_mal_formado_responde_400()
    {
        var contenido = new StringContent("{ \"email\": ", System.Text.Encoding.UTF8, "application/json");
        var r = await Cliente().PostAsync("/api/auth/login", contenido);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Abrir_turno_sin_datos_responde_422()
    {
        var token = PlantPulseApiFactory.Token(["Supervisor"], [PlantaLeon]);
        var r = await Cliente(token).PostAsJsonAsync("/api/turnos-linea", new { });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        var errores = (await Json(r)).GetProperty("errors");
        Assert.True(errores.TryGetProperty("lineaId", out _));
        Assert.True(errores.TryGetProperty("turnoId", out _));
        Assert.True(errores.TryGetProperty("fechaOperativa", out _));
    }

    [Fact]
    public async Task Registrar_produccion_con_buenas_mayores_a_totales_responde_422()
    {
        var token = PlantPulseApiFactory.Token(["Supervisor"], [PlantaLeon]);
        var r = await Cliente(token).PostAsJsonAsync($"/api/turnos-linea/{Guid.NewGuid()}/produccion",
            new { ordenProduccionId = Guid.NewGuid(), piezasTotales = 10, piezasBuenas = 11 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        Assert.True((await Json(r)).GetProperty("errors").TryGetProperty("piezasBuenas", out _));
    }

    [Fact]
    public async Task Rango_de_kpis_invertido_responde_422()
    {
        var token = PlantPulseApiFactory.Token(["Admin"], []);
        var r = await Cliente(token).GetAsync("/api/kpis/oee?desde=2026-11-04&hasta=2026-11-01");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }

    [Fact]
    public async Task Me_devuelve_roles_y_plantas_del_token()
    {
        var token = PlantPulseApiFactory.Token(["Supervisor"], [PlantaLeon]);
        var json = await Json(await Cliente(token).GetAsync("/api/auth/me"));

        Assert.Equal("Supervisor", json.GetProperty("roles")[0].GetString());
        Assert.Equal(PlantaLeon, json.GetProperty("plantaIds")[0].GetGuid());
    }
}
