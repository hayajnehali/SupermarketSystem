# TD-10 — Durable Messaging Data Model

**Version:** 0.1  
**Status:** Proposed for owner review  
**Scope:** Logical relational schema for Outbox, Inbox, processed-operation receipts, command delivery leases, and synchronization checkpoints.  
**Depends on:** Approved TD-04, TD-05, TD-07, and approved TD-09.

## 1. Design objectives

The model must provide:
- Atomic persistence of a local business change and its outbound message.
- Durable inbound deduplication and replay-safe processing.
- Separate identity for a transport message, a business operation, and a command.
- Safe recovery after process restart, network timeout, duplicate delivery, or worker crash.
- Branch isolation and independently recoverable branch databases.
- Traceability through CorrelationId and CausationId.
- No coupling between technical retry/lease timing and approved business deadlines.

This is a logical schema, not a final migration script. Every primary key is `Guid` / `uniqueidentifier`; integer identity primary keys are prohibited. SQL physical details, clustered-key choices, partitioning, and exact retention are finalized through implementation and workload testing.

## 2. Identity model

| Identifier | Purpose | Stability |
|---|---|---|
| MessageId | One immutable message envelope | Same on retransmission of that envelope |
| OperationId | One business operation/effect | Stable across all retries and message re-delivery for that operation |
| CommandId | One Central-issued command to a Branch | Stable across command delivery retries |
| CorrelationId | Groups a workflow across services/messages | Shared by related operations |
| CausationId | Identifies the message/operation that caused this message | Set when causal relationship exists |
| AggregateId | Identifies the entity/aggregate whose ordering may matter | Stable for that aggregate |
| BranchId | Tenant/ownership boundary for branch-scoped records | Derived from trusted service context |

Do not use MessageId as a substitute for OperationId. One operation may produce multiple messages; one message may be delivered many times.

## 3. Outbox table

**Primary-key convention:** `OutboxMessageId` is `Guid` in application code and `uniqueidentifier` in SQL Server. All other primary keys in this document follow the same system-wide Guid rule.

Create an Outbox in every database that originates durable messages: Central and each Branch database.

### 3.1 Logical columns

| Column | Logical type | Rules |
|---|---|---|
| OutboxMessageId | uniqueidentifier | Internal primary key (`Guid` in C#) |
| MessageId | uniqueidentifier | Required, globally unique |
| OperationId | uniqueidentifier | Required, stable business operation identity |
| MessageType | nvarchar | Required, allowlisted contract name |
| SchemaVersion | int | Required, positive |
| SourceSystem | nvarchar | Central or Branch |
| BranchId | uniqueidentifier, nullable at Central | Required for branch-originated/branch-targeted scope |
| AggregateType | nvarchar | Required when aggregate ordering applies |
| AggregateId | nvarchar | Required when aggregate ordering applies |
| AggregateSequence | bigint, nullable | Monotonic within aggregate when required |
| CorrelationId | uniqueidentifier | Required |
| CausationId | uniqueidentifier, nullable | Optional causal reference |
| Payload | nvarchar(max) | Immutable serialized contract payload |
| PayloadHash | binary/hash text | Detects payload mutation/reuse |
| OccurredAtUtc | datetimeoffset | Business event time |
| CreatedAtUtc | datetimeoffset | Durable outbox insertion time |
| Status | smallint/enum | Pending, Leased, Acknowledged, Quarantined |
| AttemptCount | int | Technical delivery attempts only |
| NextAttemptAtUtc | datetimeoffset, nullable | Retry scheduling |
| LeaseOwner | nvarchar, nullable | Worker instance holding lease |
| LeaseUntilUtc | datetimeoffset, nullable | Technical lease expiry |
| LastAttemptAtUtc | datetimeoffset, nullable | Diagnostic timestamp |
| AcknowledgedAtUtc | datetimeoffset, nullable | Set only after durable receiver acknowledgement |
| LastErrorCode | nvarchar, nullable | Safe diagnostic code |
| LastErrorAtUtc | datetimeoffset, nullable | Diagnostic timestamp |

### 3.2 Required constraints and indexes

- Primary key on OutboxId.
- Unique index on MessageId.
- Unique constraint on the producer's scoped OperationId + message purpose/type where the domain guarantees one such message. Do not impose a global one-message-per-operation constraint because one operation may legitimately emit multiple message types.
- Check constraints for positive SchemaVersion, non-negative AttemptCount, and valid status values.
- Filtered/partial index for dispatchable records by Status and NextAttemptAtUtc/CreatedAtUtc.
- Index on CorrelationId for diagnostics.
- Index on AggregateType + AggregateId + AggregateSequence where aggregate ordering is used.
- Payload is immutable after insertion; delivery metadata may change.

The originating business transaction inserts the Outbox row in the same database transaction as the business state change. A worker must never construct an outbound event only in memory after committing the business transaction.

## 4. Inbox table

Create an Inbox in every database that receives messages from another application boundary.

### 4.1 Logical columns

| Column | Logical type | Rules |
|---|---|---|
| InboxMessageId | uniqueidentifier | Internal primary key (`Guid` in C#) |
| MessageId | uniqueidentifier | Required |
| OperationId | uniqueidentifier | Required |
| MessageType | nvarchar | Required |
| SchemaVersion | int | Required |
| SourceSystem | nvarchar | Required |
| BranchId | uniqueidentifier, nullable | Required/validated for branch-scoped messages |
| CorrelationId | uniqueidentifier | Required |
| CausationId | uniqueidentifier, nullable | Optional |
| PayloadHash | binary/hash text | Required |
| ReceivedAtUtc | datetimeoffset | Required |
| ProcessingStatus | smallint/enum | Received, Processing, Applied, Rejected, Quarantined |
| ProcessedAtUtc | datetimeoffset, nullable | Set after durable outcome |
| OutcomeCode | nvarchar, nullable | Stable result code |
| OutcomePayload | nvarchar(max), nullable | Minimal safe acknowledgement result |
| LastErrorCode | nvarchar, nullable | Diagnostic code |

### 4.2 Required constraints and indexes

- Unique constraint on MessageId within the receiving database/stream scope.
- Index on OperationId and source scope for operation-level duplicate detection.
- Index on ProcessingStatus + ReceivedAtUtc for pending work.
- Index on CorrelationId for traceability.
- PayloadHash mismatch for an already-seen MessageId is a protocol/security conflict, not a duplicate success.
- Retain the immutable receipt and outcome for the configured recovery/audit horizon.

The receiver must durably record the receipt before acknowledging transport receipt. For messages that cause a local business mutation, the Inbox outcome and business mutation must commit in the same local transaction.

## 5. ProcessedOperations ledger

Use a dedicated ProcessedOperations ledger for command/business-effect deduplication where an operation can be represented by multiple transport messages or command deliveries.

### 5.1 Logical columns

| Column | Logical type | Rules |
|---|---|---|
| ProcessedOperationId | uniqueidentifier | Internal primary key (`Guid` in C#) |
| OperationId | uniqueidentifier | Required |
| OperationType | nvarchar | Required |
| ProcessingScope | nvarchar | Stable scope, e.g. Branch or Central module |
| BranchId | uniqueidentifier, nullable | Required for branch-owned operations |
| RequestHash | binary/hash text | Detects same key with different content |
| ResultCode | nvarchar | Stable recorded outcome |
| ResultPayload | nvarchar(max), nullable | Minimal replayable result |
| FirstMessageId | uniqueidentifier, nullable | Original triggering message |
| CorrelationId | uniqueidentifier | Required |
| CompletedAtUtc | datetimeoffset | Set when operation reaches durable outcome |

### 5.2 Constraints

- Unique key on ProcessingScope + BranchId (where applicable) + OperationId.
- A duplicate with the same OperationId and same RequestHash returns the stored ResultCode/ResultPayload without repeating the effect.
- Same OperationId with a different RequestHash is rejected as an idempotency conflict and audited.
- For a command that changes stock/reservation state, ProcessedOperations and the stock/reservation mutation commit in one Branch DB transaction.
- Do not expire deduplication records while a corresponding message, payment, order, or recovery replay may still arrive. Retention must be derived from the longest supported replay/recovery horizon and approved retention requirements.

If Inbox and ProcessedOperations appear redundant for a particular message type, keep both responsibilities distinct: Inbox proves receipt/transport processing of MessageId; ProcessedOperations protects the underlying OperationId effect across message identities.

## 6. Central command delivery ledger

Central needs a durable command record because polling is pull-based and delivery may repeat.

Logical table: BranchCommands.

| Column | Logical type | Rules |
|---|---|---|
| CommandId | uniqueidentifier | Primary key and stable command identity |
| BranchId | uniqueidentifier | Required target branch |
| ReservationId | uniqueidentifier, nullable | Reservation reference when relevant |
| OperationId | uniqueidentifier | Stable effect identity |
| CommandType | nvarchar | ReserveStock, CommitReservationToOrder, ReleaseReservation |
| SchemaVersion | int | Required |
| Payload | nvarchar(max) | Immutable command payload |
| PayloadHash | binary/hash text | Integrity/reuse check |
| Status | smallint/enum | Pending, Leased, Acknowledged, Rejected, Quarantined |
| CreatedAtUtc | datetimeoffset | Durable enqueue time |
| AvailableAtUtc | datetimeoffset | Technical eligibility for polling |
| LeaseOwner | nvarchar, nullable | Worker instance |
| LeaseUntilUtc | datetimeoffset, nullable | Technical lease |
| DeliveryCount | int | Technical delivery count |
| LastDeliveredAtUtc | datetimeoffset, nullable | Diagnostic |
| OutcomeCode | nvarchar, nullable | Durable Branch result |
| OutcomePayload | nvarchar(max), nullable | Minimal acknowledgement |
| AcknowledgedAtUtc | datetimeoffset, nullable | Durable acknowledgement time |

Constraints/indexes:
- Unique CommandId.
- Unique BranchId + OperationId + CommandType for command types that must be emitted once.
- Poll index on BranchId + Status + AvailableAtUtc + CreatedAtUtc.
- Index on ReservationId and CorrelationId.
- A lease expiry makes a command eligible for redelivery; it does not create a new CommandId or OperationId and does not alter a business deadline.

## 7. Synchronization checkpoints

Logical table: SyncCheckpoints.

| Column | Logical type | Rules |
|---|---|---|
| SyncCheckpointId | uniqueidentifier | Internal primary key (`Guid` in C#) |
| Direction | nvarchar | CentralToBranch or BranchToCentral |
| BranchId | uniqueidentifier | Required for branch stream |
| StreamName | nvarchar | Contract/message stream |
| LastAcknowledgedSequence | bigint, nullable | Last contiguous confirmed sequence where sequencing applies |
| LastAcknowledgedMessageId | uniqueidentifier, nullable | Last confirmed message identity |
| UpdatedAtUtc | datetimeoffset | Required |
| ConcurrencyVersion | rowversion | Optimistic concurrency |

Unique key: Direction + BranchId + StreamName.

A checkpoint advances only when the receiver has durably acknowledged the relevant contiguous sequence. Do not advance a checkpoint past a failed prerequisite in an ordered stream. Independent streams may advance independently.

## 8. Transaction boundaries

### 8.1 Originating application

Within one local database transaction:
1. Apply the business state change.
2. Insert the Outbox message(s).
3. Commit.

A rollback must leave neither the business mutation nor its outbound event.

### 8.2 Receiving application

Within one local database transaction:
1. Validate envelope and authorization before business processing.
2. Insert/claim Inbox receipt under its unique key.
3. Check ProcessedOperations for an existing result.
4. Apply the business mutation if not already processed.
5. Persist ProcessedOperations result and Inbox outcome.
6. Commit.
7. Return durable acknowledgement.

If processing requires asynchronous work, persist a durable Received state and acknowledge only receipt; do not claim the business effect is complete. A separate completion acknowledgement is sent after the effect commits.

### 8.3 Worker lease

- Claim pending records using an atomic database operation so concurrent workers cannot own the same active lease.
- Lease acquisition and status update occur in one transaction.
- A worker crash leaves the record recoverable after LeaseUntilUtc.
- A worker must be safe if it sends successfully but crashes before recording the acknowledgement; receiver idempotency handles redelivery.
- Lease duration, polling cadence, batch size, backoff, and concurrency are configurable operational settings.

## 9. Data integrity and operational safeguards

- Enforce foreign keys where records share the same database boundary; do not create cross-database foreign keys.
- Use UTC datetimeoffset values and a consistent clock source.
- Use rowversion or equivalent optimistic concurrency on mutable coordination records.
- Store payload schema version and payload hash; never mutate a message payload after enqueue.
- Encrypt databases/backups according to TD-08 and protect sensitive payload fields.
- Avoid storing full payment credentials, access tokens, OTP secrets, or unnecessary customer profile data in message payloads.
- Record state transitions with actor/service identity, timestamp, CorrelationId, OperationId, and prior/new status.
- Monitor pending age, lease expiry, repeated delivery, quarantined count, processing latency, and checkpoint lag.

## 10. Explicitly deferred implementation details

This document does not choose:
- Exact SQL Server physical types, clustered keys, partitioning, or index fill factors.
- Exact retry intervals, lease duration, polling cadence, batch sizes, or concurrency.
- Retention durations, archival schedule, and purge job configuration.
- Exact serialization library or compression/encryption envelope format.
- Exact EF Core entity names, migrations, and repository implementation.
- Isolation level/locking syntax for each operation; these must preserve the approved atomicity and concurrency invariants and be verified by integration/load tests.

These are implementation and operations details. They must not alter approved business behavior.
