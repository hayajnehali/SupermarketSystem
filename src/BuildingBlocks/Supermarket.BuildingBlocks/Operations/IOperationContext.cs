namespace Supermarket.BuildingBlocks.Operations;

/// <summary>Stable identifiers used to trace and make a business operation idempotent.</summary>
public interface IOperationContext
{
    Guid OperationId { get; }
    Guid CorrelationId { get; }
}