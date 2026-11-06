using PlantPulse.Domain.Common;

namespace PlantPulse.Domain.Produccion;

/// <summary>Se levanta al cerrar un turno. En el Día 4 dispara el refresh del dataset de Power BI.</summary>
public sealed record TurnoLineaCerrado(
    Guid TurnoLineaId,
    Guid PlantaId,
    Guid LineaId,
    DateOnly FechaOperativa,
    DateTimeOffset CerradoEnUtc) : IDomainEvent;

/// <summary>Se levanta cuando un Admin reabre un turno cerrado (queda en la bitácora de auditoría).</summary>
public sealed record TurnoLineaReabierto(
    Guid TurnoLineaId,
    Guid PlantaId,
    Guid LineaId,
    DateOnly FechaOperativa,
    DateTimeOffset ReabiertoEnUtc) : IDomainEvent;
