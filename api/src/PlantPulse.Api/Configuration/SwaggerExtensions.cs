using Microsoft.OpenApi;

namespace PlantPulse.Api.Configuration;

/// <summary>
/// Swagger con botón "Authorize" para el JWT. Escrito para Swashbuckle 10 (Microsoft.OpenApi 2.x):
/// los tipos viven en el namespace Microsoft.OpenApi y el requisito de seguridad recibe el documento.
/// </summary>
public static class SwaggerExtensions
{
    private const string Esquema = "Bearer";

    public static IServiceCollection AddPlantPulseSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "PlantPulse API",
                Version = "v1",
                Description = "KPIs de manufactura: captura de producción y paros, OEE, costo del scrap e integración con ERP y Banxico.",
            });

            c.AddSecurityDefinition(Esquema, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Pega solo el accessToken de POST /api/auth/login (sin la palabra Bearer).",
            });

            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(Esquema, document)] = [],
            });
        });
        return services;
    }
}
