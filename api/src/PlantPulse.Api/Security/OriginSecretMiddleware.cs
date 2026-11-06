using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace PlantPulse.Api.Security;

/// <summary>
/// Protege el origen (API Gateway): Cloudflare agrega <c>X-Origin-Secret</c> a cada petición y aquí se
/// rechaza con 403 lo que no lo traiga. Si <c>OriginSecret</c> no está configurado, no hace nada
/// (desarrollo, Docker y pruebas). Compara en tiempo constante.
/// </summary>
public sealed class OriginSecretMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<OriginSecretMiddleware> logger)
{
    public const string Header = "X-Origin-Secret";
    public const string ConfigKey = "OriginSecret";

    private readonly byte[]? _secreto =
        string.IsNullOrWhiteSpace(configuration[ConfigKey]) ? null : Encoding.UTF8.GetBytes(configuration[ConfigKey]!);

    public async Task InvokeAsync(HttpContext context)
    {
        if (_secreto is null || EsValido(context.Request.Headers[Header].ToString()))
        {
            await next(context);
            return;
        }

        logger.LogWarning("Petición sin {Header} válido: {Metodo} {Ruta}", Header, context.Request.Method, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = "Acceso directo al origen no permitido." },
            options: null, contentType: "application/problem+json");
    }

    private bool EsValido(string recibido) =>
        recibido.Length > 0 && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recibido), _secreto);
}
