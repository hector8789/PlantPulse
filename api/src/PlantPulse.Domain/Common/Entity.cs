namespace PlantPulse.Domain.Common;

/// <summary>Entidad con identidad propia. La igualdad se define por tipo e Id.</summary>
public abstract class Entity : IEquatable<Entity>
{
    public Guid Id { get; protected init; }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
            throw new DomainException("entidad.id_vacio", "El identificador de la entidad no puede ser vacío.");
        Id = id;
    }

    /// <summary>Solo para EF Core.</summary>
    protected Entity() { }

    /// <summary>Ids v7: ordenados por tiempo, mejor localidad en los índices de PostgreSQL.</summary>
    protected static Guid NuevoId() => Guid.CreateVersion7();

    public bool Equals(Entity? other) =>
        other is not null && other.GetType() == GetType() && other.Id == Id && Id != Guid.Empty;

    public override bool Equals(object? obj) => obj is Entity e && Equals(e);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
