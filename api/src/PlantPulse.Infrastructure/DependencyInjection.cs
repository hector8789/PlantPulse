using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using PlantPulse.Application.Common;
using PlantPulse.Application.PowerBi;
using PlantPulse.Infrastructure.Externos;
using PlantPulse.Infrastructure.Persistence;
using PlantPulse.Infrastructure.Persistence.Repositorios;
using PlantPulse.Infrastructure.Persistence.Seed;
using PlantPulse.Infrastructure.PowerBi;
using PlantPulse.Infrastructure.Security;

namespace PlantPulse.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Nombre de la cadena en user-secrets: <c>ConnectionStrings:PlantPulse</c>.</summary>
    public const string ConnectionStringName = "PlantPulse";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Reloj inyectable: los handlers lo usan para "ahora" y las pruebas lo pueden fijar.
        services.TryAddSingleton(TimeProvider.System);

        AddPersistence(services, configuration);
        AddExternos(services, configuration);

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        AddPowerBi(services, configuration);
        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        // La cadena se lee al crear el primer DbContext, no al registrar: así las pruebas
        // que no tocan la base (CORS, auth, ERP simulado) arrancan sin PostgreSQL.
        services.AddDbContext<PlantPulseDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    $"Falta la cadena de conexión 'ConnectionStrings:{ConnectionStringName}'. " +
                    "Configúrala con: dotnet user-secrets set \"ConnectionStrings:PlantPulse\" \"Host=...\" --project src/PlantPulse.Api");

            var reloj = sp.GetRequiredService<TimeProvider>();

            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .UseSeeding((context, _) => PlantPulseSeeder.Sembrar(context, reloj.GetUtcNow()))
                .UseAsyncSeeding((context, _, ct) => PlantPulseSeeder.SembrarAsync(context, reloj.GetUtcNow(), ct));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPlantaRepository, PlantaRepository>();
        services.AddScoped<ILineaRepository, LineaRepository>();
        services.AddScoped<ICausaParoRepository, CausaParoRepository>();
        services.AddScoped<IOrdenProduccionRepository, OrdenProduccionRepository>();
        services.AddScoped<ITurnoLineaRepository, TurnoLineaRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<ITipoCambioRepository, TipoCambioRepository>();
    }

    private static void AddExternos(IServiceCollection services, IConfiguration configuration)
    {
        // Se leen al resolver el cliente (no al registrar), así toman la configuración final.
        services.AddTransient(_ => new BanxicoSettings
        {
            BaseUrl = configuration[$"{BanxicoSettings.SectionName}:BaseUrl"] ?? new BanxicoSettings().BaseUrl,
            Token = configuration[$"{BanxicoSettings.SectionName}:Token"],
        });

        services.AddHttpClient<IErpOrdenesClient, ErpODataClient>(http =>
            {
                var baseUrl = configuration[$"{ErpSettings.SectionName}:BaseUrl"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                    throw new InvalidOperationException(
                        "Falta 'Erp:BaseUrl' (p. ej. https://localhost:7001/erp/odata/) en appsettings.Development.json.");
                http.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
            })
            .AddStandardResilienceHandler(ConfigurarResiliencia);

        services.AddHttpClient<ITipoCambioProvider, BanxicoClient>((sp, http) =>
            {
                var baseUrl = sp.GetRequiredService<BanxicoSettings>().BaseUrl;
                http.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
            })
            .AddStandardResilienceHandler(ConfigurarResiliencia);
    }

    private static void AddPowerBi(IServiceCollection services, IConfiguration configuration)
    {
        // Singleton: MSAL guarda en caché el token del service principal mientras viva la instancia.
        services.AddSingleton(_ => PowerBiSettings.Desde(configuration));
        services.AddSingleton<PowerBiAuth>();
        services.AddSingleton<PowerBiEmbedService>();
        services.AddSingleton<IPowerBiEmbedService>(sp => sp.GetRequiredService<PowerBiEmbedService>());
        services.AddSingleton<IPowerBiDatasetRefresher>(sp => sp.GetRequiredService<PowerBiEmbedService>());

        // Scoped: usa el DbContext de la petición. El estado del refresh vive en la base (sirve en Lambda).
        services.AddScoped<IPowerBiRefreshScheduler, PowerBiRefreshCoordinator>();
    }

    /// <summary>
    /// Polly v8 vía Microsoft.Extensions.Http.Resilience. Orden del pipeline estándar:
    /// timeout total → reintentos → circuit breaker → timeout por intento.
    /// </summary>
    internal static void ConfigurarResiliencia(HttpStandardResilienceOptions o)
    {
        o.Retry.MaxRetryAttempts = 3;
        o.Retry.BackoffType = DelayBackoffType.Exponential;
        o.Retry.UseJitter = true;
        o.Retry.Delay = TimeSpan.FromMilliseconds(500);        // ~0.5 s, 1 s, 2 s (+ jitter)

        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);

        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);   // debe ser ≥ 2 × AttemptTimeout
        o.CircuitBreaker.FailureRatio = 0.5;
        o.CircuitBreaker.MinimumThroughput = 5;
        o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    }
}
