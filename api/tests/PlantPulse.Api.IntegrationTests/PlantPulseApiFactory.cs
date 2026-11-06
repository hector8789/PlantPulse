using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace PlantPulse.Api.IntegrationTests;

/// <summary>
/// API en memoria con configuración de prueba. No necesita PostgreSQL: las pruebas que la usan
/// solo tocan autenticación, autorización, validación (422) y el ERP simulado.
/// </summary>
public sealed class PlantPulseApiFactory : WebApplicationFactory<Program>
{
    public const string LlaveJwt = "llave-solo-para-pruebas-de-integracion-plantpulse-2026";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = LlaveJwt,
            ["ConnectionStrings:PlantPulse"] = "Host=localhost;Database=plantpulse_pruebas_sin_bd;Username=x;Password=x",
            ["Erp:BaseUrl"] = "http://localhost/erp/odata/",
            ["ErpSimulado:TasaFallas"] = "0",
            ["ErpSimulado:LatenciaMs"] = "0",
            // Power BI sin credenciales de Azure: el embed responde 503 y el refresh se desactiva.
            ["PowerBi:TenantId"] = "",
            ["PowerBi:ClientId"] = "",
            ["PowerBi:ClientSecret"] = "",
            ["PowerBi:WorkspaceId"] = "",
            ["PowerBi:Refresh:Habilitado"] = "false",
            ["PowerBi:Reportes:oee:Nombre"] = "OEE por línea y turno",
            ["PowerBi:Reportes:oee:ReportId"] = "00000000-0000-0000-0000-000000000001",
            ["PowerBi:Reportes:pareto-paros:Nombre"] = "Pareto de paros",
            ["PowerBi:Reportes:pareto-paros:ReportId"] = "00000000-0000-0000-0000-000000000002",
        }));
    }

    /// <summary>Genera un JWT igual al del login (mismo issuer, audience y claims).</summary>
    public static string Token(string[] roles, Guid[] plantas, string llave = LlaveJwt)
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()), new("name", "Prueba") };
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        claims.AddRange(plantas.Select(p => new Claim("planta", p.ToString())));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = "PlantPulse.Api",
            Audience = "PlantPulse.Web",
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(llave)), SecurityAlgorithms.HmacSha256),
        });
    }
}
