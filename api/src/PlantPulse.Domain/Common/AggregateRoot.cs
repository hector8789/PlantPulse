namespace PlantPulse.Domain.Common;

/// <summary>
/// Raíz de agregado: frontera de consistencia y única puerta de entrada para modificar sus hijos.
/// Acumula eventos de dominio que Application publicará después de guardar (Día 2 en adelante).
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(Guid id) : base(id) { }

    protected AggregateRoot() { }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
