using System.Net;
using Microsoft.Extensions.Configuration;
using PlantPulse.Api.Security;

namespace PlantPulse.Api.IntegrationTests;

public sealed class OriginSecretTests(PlantPulseApiFactory factory) : IClassFixture<PlantPulseApiFactory>
{
    private const string Secreto = "secreto-de-origen-solo-para-pruebas";

    private HttpClient ClienteConSecretoConfigurado() =>
        factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
                c.AddInMemoryCollection(new Dictionary<string, string?> { [OriginSecretMiddleware.ConfigKey] = Secreto })))
            .CreateClient();

    [Fact]
    public async Task Sin_el_header_responde_403()
    {
        var response = await ClienteConSecretoConfigurado().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Con_un_valor_incorrecto_responde_403()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add(OriginSecretMiddleware.Header, "otro-valor");
        Assert.Equal(HttpStatusCode.Forbidden, (await ClienteConSecretoConfigurado().SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Con_el_header_correcto_pasa()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add(OriginSecretMiddleware.Header, Secreto);
        Assert.Equal(HttpStatusCode.OK, (await ClienteConSecretoConfigurado().SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Sin_secreto_configurado_no_filtra()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
