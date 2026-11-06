namespace PlantPulse.Domain.Common;

/// <summary>
/// Violación de una regla de negocio. La API la traduce a 409 Conflict (Día 2).
/// <see cref="Codigo"/> es estable y el frontend puede usarlo para mostrar mensajes propios.
/// </summary>
public class DomainException(string codigo, string mensaje) : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
}
