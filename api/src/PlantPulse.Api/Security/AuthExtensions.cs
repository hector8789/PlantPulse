using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PlantPulse.Application.Common;
using PlantPulse.Domain.Usuarios;

namespace PlantPulse.Api.Security;

/// <summary>Sección "Jwt". La llave va SOLO en user-secrets; Issuer, Audience y expiración tienen valores por defecto.</summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";
    public const int LongitudMinimaLlave = 32;   // HS256 exige al menos 256 bits

    public string Issuer { get; set; } = "PlantPulse.Api";
    public string Audience { get; set; } = "PlantPulse.Web";
    public string? Key { get; set; }
    public int ExpiracionMinutos { get; set; } = 480;

    public SymmetricSecurityKey CrearLlave()
    {
        if (string.IsNullOrWhiteSpace(Key) || Encoding.UTF8.GetByteCount(Key) < LongitudMinimaLlave)
            throw new InvalidOperationException(
                $"Falta 'Jwt:Key' o tiene menos de {LongitudMinimaLlave} bytes. " +
                "Configúrala con: dotnet user-secrets set \"Jwt:Key\" \"<llave larga>\" --project src/PlantPulse.Api");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
    }
}

/// <summary>Nombres de los claims del JWT. Se usan tal cual (MapInboundClaims = false).</summary>
public static class ClaimsPlantPulse
{
    public const string Sub = "sub";
    public const string Nombre = "name";
    public const string Email = "email";
    public const string Rol = "role";
    public const string Planta = "planta";
}

public static class Politicas
{
    /// <summary>Abrir turnos, capturar producción y paros, cerrar turnos, sincronizar el ERP.</summary>
    public const string Captura = "Captura";

    /// <summary>Reabrir turnos.</summary>
    public const string SoloAdmin = "SoloAdmin";
}

public static class AuthExtensions
{
    public static IServiceCollection AddPlantPulseAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>().Bind(configuration.GetSection(JwtSettings.SectionName));
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Se configura al resolver las opciones (no al arrancar), así toma la configuración final,
        // incluida la de las pruebas de integración.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, jwt) =>
            {
                var s = jwt.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = s.Issuer,
                    ValidateAudience = true,
                    ValidAudience = s.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = s.CrearLlave(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimsPlantPulse.Nombre,
                    RoleClaimType = ClaimsPlantPulse.Rol,
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.Captura, p => p.RequireRole(Roles.Admin, Roles.Supervisor))
            .AddPolicy(Politicas.SoloAdmin, p => p.RequireRole(Roles.Admin));

        return services;
    }
}
