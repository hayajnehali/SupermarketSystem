namespace Central.Domain.Common;

/// <summary>
/// Base for domain entities with GUID identity.
/// Equality is based on the concrete entity type and a non-empty identifier.
/// </summary>
public abstract class EntityBase : IEquatable<EntityBase>
{
    protected EntityBase()
    {
    }

    protected EntityBase(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Entity ID cannot be empty.", nameof(id));

        Id = id;
    }

    public Guid Id { get; private set; }

    public bool Equals(EntityBase? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null || GetType() != other.GetType())
            return false;

        return Id != Guid.Empty && other.Id != Guid.Empty && Id == other.Id;
    }

    public override bool Equals(object? obj)
        => obj is EntityBase other && Equals(other);

    // Keep the hash stable while EF Core materializes private-set identifiers.
    // Equal entities always share the same concrete type and therefore the same hash.
    public override int GetHashCode() => GetType().GetHashCode();
}
