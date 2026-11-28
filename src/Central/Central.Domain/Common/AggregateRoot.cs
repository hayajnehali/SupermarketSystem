namespace Central.Domain.Common;

/// <summary>
/// Marks an entity as the consistency and repository boundary of an aggregate.
/// Only aggregate roots receive repositories; child entities are changed through their root.
/// </summary>
public abstract class AggregateRoot : EntityBase
{
    protected AggregateRoot()
    {
    }

    protected AggregateRoot(Guid id) : base(id)
    {
    }
}
