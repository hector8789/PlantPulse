namespace PlantPulse.Application.Common;

// La API traduce cada excepción a ProblemDetails (ver PlantPulse.Api/Errors).

/// <summary>404: el recurso no existe.</summary>
public sealed class NotFoundException(string recurso, object clave)
    : Exception($"{recurso} '{clave}' no existe.")
{
    public string Recurso { get; } = recurso;
    public object Clave { get; } = clave;
}

/// <summary>403: el usuario está autenticado pero no tiene acceso (rol o planta ajena).</summary>
public sealed class ForbiddenAccessException(string mensaje = "No tienes acceso a este recurso.") : Exception(mensaje);

/// <summary>401: credenciales inválidas.</summary>
public sealed class UnauthorizedException(string mensaje) : Exception(mensaje);

/// <summary>409: conflicto que no viene del dominio (registro duplicado, carrera con otro usuario).</summary>
public sealed class ConflictException(string codigo, string mensaje) : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
}

/// <summary>503: un servicio externo (ERP, Banxico) no respondió aun después de los reintentos.</summary>
public sealed class ExternalServiceException(string servicio, string mensaje, Exception? inner = null)
    : Exception(mensaje, inner)
{
    public string Servicio { get; } = servicio;
}
