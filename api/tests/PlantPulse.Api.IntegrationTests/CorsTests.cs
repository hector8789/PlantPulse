using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlantPulse.Api.IntegrationTests;

public sealed class CorsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ReactOrigin = "http://localhost:5173";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Preflight_desde_React_es_aceptado()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/health");
        request.Headers.Add("Origin", ReactOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(ReactOrigin, Assert.Single(response.Headers.GetValues(AllowOriginHeader)));
    }

    [Fact]
    public async Task Preflight_desde_origen_no_permitido_no_recibe_header_CORS()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/health");
        request.Headers.Add("Origin", "http://sitio-malicioso.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains(AllowOriginHeader));
    }

    [Fact]
    public async Task Get_health_desde_React_responde_200_con_header_CORS()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("Origin", ReactOrigin);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ReactOrigin, Assert.Single(response.Headers.GetValues(AllowOriginHeader)));

        var body = await response.Content.ReadFromJsonAsync<HealthBody>();
        Assert.Equal("Healthy", body?.Status);
    }

    private sealed record HealthBody(string Status, string Service, DateTime TimestampUtc);
}
