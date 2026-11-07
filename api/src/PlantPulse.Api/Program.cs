using PlantPulse.Api.Configuration;
using PlantPulse.Api.Errors;
using PlantPulse.Api.Security;
using PlantPulse.Application;
using PlantPulse.Infrastructure;
using PlantPulse.Infrastructure.Persistence;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- AWS Lambda -----------------------------------------------
// Lambda define AWS_LAMBDA_FUNCTION_NAME. Fuera de Lambda (local, Docker, pruebas) nada de esto aplica.
var enLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));
if (enLambda)
{
    // Secretos en SSM Parameter Store: /plantpulse/ConnectionStrings/PlantPulse → ConnectionStrings:PlantPulse.
    builder.Configuration.AddSystemsManager(builder.Configuration["Aws:SsmPrefijo"] ?? "/plantpulse");

    // En Lambda el origen solo se alcanza a través de Cloudflare (o con el secreto): sin él, no arranca.
    if (string.IsNullOrWhiteSpace(builder.Configuration[OriginSecretMiddleware.ConfigKey]))
        throw new InvalidOperationException(
            $"Falta '{OriginSecretMiddleware.ConfigKey}' en SSM: en Lambda es obligatorio.");
}
// Reemplaza Kestrel por el runtime de Lambda (API Gateway HTTP API). Fuera de Lambda no hace nada.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

// --- Capas ----------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// --- API ------------------------------------------------------
builder.Services.AddPlantPulseCors(builder.Configuration, builder.Environment);
builder.Services.AddPlantPulseAuth(builder.Configuration);     // JWT + políticas Captura / SoloAdmin

// Sin [Required] implícito en strings no anulables: así un campo faltante llega a FluentValidation
// y responde 422 (contrato), no 400. El JSON mal formado sigue respondiendo 400.
builder.Services.AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PlantPulseExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddPlantPulseSwagger();

var app = builder.Build();

// Opcional (docker-compose): aplica las migraciones y siembra los datos demo al arrancar.
// En desarrollo local se sigue usando "dotnet ef database update".
if (app.Configuration.GetValue<bool>("Database:AplicarMigraciones"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PlantPulseDbContext>().Database.MigrateAsync();
}

// --- Pipeline (el orden importa) -----------------------------
app.UseExceptionHandler();

// Solo si hay OriginSecret configurado: rechaza con 403 lo que no llegue por Cloudflare.
app.UseMiddleware<OriginSecretMiddleware>();

app.UseStatusCodePages();   // 401/403/404 sin cuerpo → ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// En Lambda el TLS termina en API Gateway; la redirección solo aplica al correr con Kestrel.
if (!enLambda) app.UseHttpsRedirection();

// CORS antes de autenticación/autorización: el preflight (OPTIONS)
// llega sin token y debe responderse aquí.
app.UseCors(CorsExtensions.FrontendPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Necesario para WebApplicationFactory<Program> en las pruebas de integración.
public partial class Program;
