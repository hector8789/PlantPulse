namespace PlantPulse.Api.Configuration;

public static class CorsExtensions
{
    public const string FrontendPolicy = "PlantPulseFrontend";

    /// <summary>Origen de Vite en desarrollo, usado si no hay configuración.</summary>
    private const string ViteDevOrigin = "http://localhost:5173";

    public static IServiceCollection AddPlantPulseCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>() ?? new CorsSettings();

        // "http://localhost:5173/" no coincide con el header Origin "http://localhost:5173":
        // se normaliza quitando la diagonal final.
        var origins = settings.AllowedOrigins
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (origins.Length == 0)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"Configura '{CorsSettings.SectionName}:AllowedOrigins' para el entorno {environment.EnvironmentName}.");
            }

            origins = [ViteDevOrigin];
        }

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendPolicy, policy => policy
                .WithOrigins(origins)
                .AllowAnyHeader()                         // Authorization, Content-Type
                .AllowAnyMethod()                         // GET, POST, PUT, DELETE, PATCH
                .WithExposedHeaders("Content-Disposition") // nombre de archivo en exportaciones a Excel
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
            // Sin AllowCredentials: el JWT viaja en el header Authorization, no en cookies.
        });

        return services;
    }
}
