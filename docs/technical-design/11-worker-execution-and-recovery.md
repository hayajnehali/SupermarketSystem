# TD-11 — Worker Execution and Recovery Model

**Version:** 0.1  
**Status:** Approved by owner (2026-11-06)  
**Scope:** Runtime behavior for Central.Worker and Branch.Worker when polling, claiming, processing, acknowledging, retrying, and recovering durable work.  
**Depends on:** TD-05, TD-09, and the proposed logical data model in TD-10.

## 1. Purpose and boundaries

This document turns the approved pull-based synchronization architecture into a deterministic worker execution model. It covers technical delivery mechanics only. It does not define or change business deadlines, order outcomes, reservation policy, refund policy, or offline operating permissions.

The database remains the durable source of work. In-memory queues may improve throughput but must never be the only record of pending work.

## 2. Worker responsibilities

### Central.Worker
- Select eligible commands for a specific target Branch.
- Atomically claim a bounded batch using a lease.
- Return stable CommandId, OperationId, schema version, payload hash, and correlation metadata to the authenticated Branch.Worker.
- Accept durable command outcomes and persist them before acknowledging the response.
- Publish branch-originated synchronization messages to their owning processing pipeline.

### Branch.Worker
- Authenticate to Central and identify its BranchId from trusted credentials, not request payload.
- Poll only for commands addressed to that BranchId.
- Validate envelope version, payload hash, command type allowlist, and required identifiers.
- Submit commands to Branch.Api/local application processing.
- Send the durable outcome to Central; retain/retry the acknowledgement if Central is unavailable.
- Publish branch-originated messages using the local Outbox.

A worker does not directly modify another application's database.

## 3. Poll and claim lifecycle

1. Worker authenticates and requests eligible work for its assigned scope.
2. Server selects records that are pending and available, or whose prior lease has expired.
3. Selection and lease assignment occur atomically in one database transaction.
4. The server returns only records successfully claimed by that worker.
5. The worker processes each record independently unless an explicitly ordered aggregate stream requires sequential processing.
6. A successful durable receiver outcome is recorded against the same stable message/command identity.
7. The lease is cleared or the record transitions to its terminal delivery state.

Concurrent workers must not both hold a valid lease for the same record. SQL Server implementations must use an atomic claim pattern with appropriate row-level locking and update semantics; a read-then-update sequence without concurrency protection is prohibited.

## 4. Lease behavior

- LeaseOwner identifies the worker instance; LeaseUntilUtc is stored in UTC.
- Lease duration and renewal cadence are configurable operational settings.
- A worker may renew only a lease it currently owns and only while the record remains eligible for processing.
- A worker that loses its lease must stop initiating new work for that record.
- Lease expiry permits redelivery; it does not prove that the previous attempt failed.
- A late acknowledgement is accepted only if it matches the stable record identity and a valid recorded outcome. It must not overwrite a newer conflicting terminal outcome.
- Clock skew is mitigated by evaluating lease expiry using the database server's UTC clock.

## 5. Delivery and processing outcomes

Keep transport delivery state separate from business processing outcome.

Suggested technical delivery outcomes:
- Pending: eligible for claim when AvailableAtUtc is reached.
- Leased: currently assigned to a worker.
- Delivered: transport request was sent; durable business completion is not implied.
- Acknowledged: receiver persisted a durable outcome.
- RetryScheduled: temporary technical failure; next attempt is scheduled.
- Quarantined: automatic processing stopped because the record is malformed, incompatible, or repeatedly fails a non-transient validation.

The receiver's domain outcome (for example, applied or rejected) is stored separately from delivery state. A technically acknowledged rejection is still a successfully delivered message.

## 6. Retry and backoff

Classify failures before scheduling another attempt:

| Failure class | Technical handling |
|---|---|
| Network timeout / connection failure | Retry with bounded exponential backoff and jitter |
| HTTP 429 | Respect Retry-After when valid; otherwise backoff |
| HTTP 5xx | Retry with bounded exponential backoff and jitter |
| Authentication failure | Stop rapid retries; emit security/availability alert and retry only under configured policy |
| Invalid envelope / unsupported schema | Quarantine; do not retry unchanged payload indefinitely |
| Payload hash or identity conflict | Quarantine and raise a high-severity integrity alert |
| Durable business rejection | Record the receiver outcome; do not treat as transport failure |

Maximum attempts, backoff bounds, jitter, and alert thresholds are configurable. Technical retries preserve MessageId, CommandId, and OperationId. A retry must never create a new business operation.

## 7. Idempotency and crash windows

The design must remain correct across these crash windows:

- Crash before claim commit: record remains eligible.
- Crash after claim but before send: lease expires and record is redelivered.
- Receiver commits but response is lost: sender retries the same identity; receiver returns the recorded outcome without repeating the effect.
- Sender receives response but crashes before recording acknowledgement: sender retries; receiver idempotency prevents duplicate effects.
- Worker restarts during processing: unfinished work is recovered through lease expiry and durable Inbox/operation state.

A transport acknowledgement is valid only after the receiver has durably stored the receipt or processing outcome, according to the API contract's acknowledgement level.

## 8. Ordering and concurrency

- Default processing is independent per message.
- Where aggregate ordering is required, use AggregateType + AggregateId + AggregateSequence and do not dispatch a later sequence while a required earlier sequence remains unresolved.
- Do not impose global ordering across unrelated branches or streams.
- Per-branch concurrency, batch size, and per-aggregate serialization are configurable.
- Duplicate and out-of-order deliveries must be safe through identity checks and receiver-side idempotency.

## 9. Cancellation and shutdown

- Workers stop claiming new work when graceful shutdown begins.
- In-flight work is allowed a bounded configurable drain period.
- If work cannot finish within the drain period, the worker must not mark it acknowledged; lease expiry enables recovery.
- Cancellation tokens must flow through network and database operations.
- Process termination must not delete or reset durable work records.

## 10. Observability

Emit structured logs and metrics with MessageId, CommandId where applicable, OperationId, CorrelationId, BranchId, MessageType, SchemaVersion, worker instance, attempt number, and outcome code.

Minimum metrics:
- Pending count and oldest pending age.
- Active and expired leases.
- Delivery attempts and retry rate by failure class.
- Quarantined records.
- Processing and acknowledgement latency.
- Per-branch polling health and synchronization lag.
- Idempotent duplicate count and identity/hash conflicts.

Logs must exclude credentials, access tokens, payment secrets, OTP values, and unnecessary customer data. Alert thresholds are configuration, not business rules.

## 11. Configuration and implementation validation

Operational settings include polling interval, batch size, lease duration, lease renewal interval, maximum concurrency, backoff bounds, maximum technical attempts, drain period, and alert thresholds.

Validate the implementation with integration tests for concurrent claims, worker crash/restart, lost acknowledgements, duplicate delivery, late acknowledgement, expired lease, unsupported schema, hash mismatch, ordered aggregates, and branch-scope isolation. Load tests must verify database claim-query performance and queue growth behavior.

## 12. Explicit exclusions

This document does not change:
- Approved offline duration or the Central-controlled offline resumption gate.
- Reservation, payment, order, cancellation, delivery, or refund business rules.
- Any customer-facing deadline or business attempt count.
- Branch stock authority or Central/Branch data ownership.

TD-11 defines technical worker behavior only.