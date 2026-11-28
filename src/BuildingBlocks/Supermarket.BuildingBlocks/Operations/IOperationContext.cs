namespace Supermarket.BuildingBlocks.Operations;

/// <summary>Stable identifiers and device metadata used to trace the current business operation.</summary>
public interface IOperationContext
{
    Guid OperationId { get; }
    Guid CorrelationId { get; }
    string? DeviceId { get; }
}