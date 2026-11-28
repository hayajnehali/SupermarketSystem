namespace Supermarket.Contracts.Integration;

/// <summary>
/// Versioned transport envelope for durable Central/Branch integration messages.
/// Payloads are contracts, never EF entities or Domain objects.
/// </summary>
public sealed record IntegrationEnvelope<TPayload>(
    Guid MessageId,
    Guid OperationId,
    Guid CorrelationId,
    string ContractName,
    int SchemaVersion,
    string SourceSystem,
    Guid? SourceBranchId,
    DateTimeOffset OccurredAtUtc,
    TPayload Payload)
{
    public static IntegrationEnvelope<TPayload> Create(
        Guid operationId,
        Guid correlationId,
        string contractName,
        int schemaVersion,
        string sourceSystem,
        Guid? sourceBranchId,
        DateTimeOffset occurredAtUtc,
        TPayload payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);

        if (operationId == Guid.Empty)
            throw new ArgumentException("OperationId cannot be empty.", nameof(operationId));

        if (correlationId == Guid.Empty)
            throw new ArgumentException("CorrelationId cannot be empty.", nameof(correlationId));

        if (schemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));

        if (occurredAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("OccurredAtUtc must use the UTC offset.", nameof(occurredAtUtc));

        return new IntegrationEnvelope<TPayload>(
            Guid.NewGuid(),
            operationId,
            correlationId,
            contractName,
            schemaVersion,
            sourceSystem,
            sourceBranchId,
            occurredAtUtc,
            payload);
    }
}