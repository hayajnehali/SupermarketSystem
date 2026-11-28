namespace Supermarket.BuildingBlocks.Operations;

/// <summary>
/// Identifies the current execution for tracing. Idempotency is enforced separately
/// through an explicit idempotency key or processed-operation record.
/// </summary>
public interface IOperationContext
{
    Guid OperationId { get; }
    Guid CorrelationId { get; }

    /// <summary>
    /// Authenticated device identifier when the operation originated from a trusted device;
    /// null for non-device and background operations.
    /// </summary>
    string? DeviceId { get; }
}
