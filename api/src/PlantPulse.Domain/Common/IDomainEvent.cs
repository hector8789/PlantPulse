namespace PlantPulse.Domain.Common;

/// <summary>
/// Marcador de evento de dominio. No depende de MediatR: Application lo envuelve en una
/// notificación para mantener el dominio libre de paquetes externos.
/// </summary>
public interface IDomainEvent;
