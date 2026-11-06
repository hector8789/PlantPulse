using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlantPulse.Application.Common;
using PlantPulse.Domain.Common;

namespace PlantPulse.Api.Errors;

/// <summary>
/// Traduce las excepciones a ProblemDetails según el contrato:
/// 400 petición mal formada · 401 credenciales · 403 sin permiso o planta ajena · 404 no existe ·
/// 409 regla de negocio o duplicado (con "codigo") · 422 validación (errors por campo en camelCase) ·
/// 503 servicio externo caído. Lo demás cae al 500 genérico de UseExceptionHandler.
/// </summary>
public sealed class PlantPulseExceptionHandler(IProblemDetailsService problemDetails, ILogger<PlantPulseExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var pd = Crear(exception);
        if (pd is null) return false;

        if (pd.Status >= 500) logger.LogWarning(exception, "Servicio externo no disponible.");

        httpContext.Response.StatusCode = pd.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = pd,
            Exception = exception,
        });
    }

    internal static ProblemDetails? Crear(Exception exception) => exception switch
    {
        ValidationException v => Validacion(v),
        DomainException d => ConCodigo(StatusCodes.Status409Conflict, "Regla de negocio", d.Message, d.Codigo),
        ConflictException c => ConCodigo(StatusCodes.Status409Conflict, "Conflicto", c.Message, c.Codigo),
        NotFoundException n => Simple(StatusCodes.Status404NotFound, "No encontrado", n.Message),
        ForbiddenAccessException f => Simple(StatusCodes.Status403Forbidden, "Sin permiso", f.Message),
        UnauthorizedException u => Simple(StatusCodes.Status401Unauthorized, "No autorizado", u.Message),
        ExternalServiceException e => Externo(e),
        BadHttpRequestException b => Simple(StatusCodes.Status400BadRequest, "Petición mal formada", b.Message),
        _ => null,
    };

    private static ValidationProblemDetails Validacion(ValidationException ex)
    {
        var errores = ex.Errors
            .GroupBy(e => CamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errores)
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Uno o más datos no son válidos.",
        };
    }

    private static ProblemDetails Simple(int status, string titulo, string detalle) =>
        new() { Status = status, Title = titulo, Detail = detalle };

    private static ProblemDetails ConCodigo(int status, string titulo, string detalle, string codigo)
    {
        var pd = Simple(status, titulo, detalle);
        pd.Extensions["codigo"] = codigo;
        return pd;
    }

    private static ProblemDetails Externo(ExternalServiceException e)
    {
        var pd = Simple(StatusCodes.Status503ServiceUnavailable, "Servicio externo no disponible", e.Message);
        pd.Extensions["servicio"] = e.Servicio;
        return pd;
    }

    /// <summary>"PiezasBuenas" → "piezasBuenas"; "Lineas[0].Codigo" → "lineas[0].codigo".</summary>
    private static string CamelCase(string propiedad) =>
        string.Join('.', propiedad.Split('.').Select(p => JsonNamingPolicy.CamelCase.ConvertName(p)));
}
