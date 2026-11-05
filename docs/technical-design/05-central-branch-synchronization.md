# Technical Design — Central/Branch Synchronization

**Document:** TD-05  
**Version:** 0.1  
**Status:** Initial synchronization design proposal — pending owner review  
**Depends on:** Merged BRS, approved owner decisions, TD-01 Architecture Overview, TD-03 Backend Modules, TD-04 Database Topology and Data Ownership.

## 1. Purpose and invariants

This document defines how the Central application and each Branch application exchange durable business data while preserving the approved ownership rules and Offline policy.

The design must guarantee:

1. No direct SQL/database connection between Central and Branch sites.
2. A branch records local operations durably before attempting to transmit them.
3. Repeated delivery of the same message does not repeat its business effect.
4. Acknowledgement is issued only after the receiver durably records the processing outcome.
5. Conflicts are isolated; unrelated valid operations continue.
6. Centrally governed data is published from Central as approved, versioned data.
7. Branch operational facts are transmitted with branch, actor, device, timestamp, and operation identity.
8. Synchronization recovery does not itself authorize POS sales to resume.
9. The centrally configured Offline duration applies uniformly to all branches.
10. Delivery policy remains one delivery attempt; this is separate from technical message retry.

## 2. Communication model

- Each Branch Worker initiates authenticated synchronization with Central endpoints.
- Central does not connect directly to a branch database or initiate database-level replication.
- Synchronization may use HTTPS APIs with bounded request/response batches. A transport technology and endpoint contract will be fixed in a later API design.
- Central-to-Branch data is delivered as versioned publication packages or messages.
- Branch-to-Central data is delivered as durable operation messages.
- Both directions use message identity, correlation, schema version, and durable processing receipts.

A branch may temporarily be unable to reach Central. Its Worker retains pending outbound messages and resumes from its last confirmed checkpoint when connectivity returns, subject to the centrally configured Offline policy.

## 3. Message envelope

Every synchronizable message must carry the following logical metadata:

| Field | Meaning |
|---|---|
| MessageId | Unique identity for this transport message |
| OperationId | Stable identity of the underlying business operation; reused across retries |
| MessageType | Contract/event type |
| SchemaVersion | Version of the message contract |
| SourceSystem | Central or Branch |
| BranchId | Source/target branch where applicable |
| OccurredAtUtc | Time the business event occurred |
| CreatedAtUtc | Time the message was recorded for delivery |
| CorrelationId | Groups messages belonging to one business workflow |
| CausationId | Identifies the message/operation that caused this message, when applicable |
| Sequence/Checkpoint | Ordering/checkpoint metadata where the contract requires it |
| Payload | Versioned business data; contains no unnecessary personal data |

The receiver must validate message type/version, source identity, branch scope, required fields, and authorization before applying the business effect.

## 4. Durable Outbox and Inbox

### 4.1 Outbox

- The business transaction and its Outbox record are committed in the same local database transaction.
- A Worker reads pending Outbox records and sends them in bounded batches.
- A record is not marked delivered merely because an HTTP request was sent.
- It is marked acknowledged only after a durable receiver acknowledgement is received and recorded.
- Failed transport attempts leave the message pending for retry.
- The Outbox retains enough status and diagnostic information for support and reconciliation.

### 4.2 Inbox / processed-message ledger

- The receiver checks whether the MessageId or OperationId has already been processed.
- A previously completed operation returns its recorded result without repeating the business effect.
- The receiver commits the business change and durable processing receipt atomically within its database boundary.
- Invalid or conflicting messages are recorded with an explicit outcome; they are not silently discarded.
- Inbox retention must cover the agreed operational, audit, and recovery window; exact retention is a later operational policy decision.

### 4.3 Idempotency

- OperationId is stable for one business operation and must not be regenerated on retry.
- MessageId identifies one message envelope; a retransmission may use the same MessageId.
- Unique constraints protect processed OperationIds in the relevant scope.
- Handlers must be designed so duplicate delivery cannot create duplicate invoices, stock movements, reservations, payments, refunds, loyalty entries, or approvals.
- External provider operations must also use provider idempotency keys where supported, with internal idempotency records regardless.

## 5. Synchronization directions

### 5.1 Central to Branch

Examples include:

- Approved product/catalogue changes.
- Approved unit/barcode/composition changes.
- Approved base prices and promotions, including effective dates and expiry.
- Employee/role/permission and device authorization snapshots.
- Branch configuration and centrally controlled Offline policy.
- Assigned online-order and fulfillment instructions.
- Approved stock transfer instructions and centrally approved actions.
- Central decisions, including conflict outcomes and explicit authorization to resume sales.

Each publication has a version or sequence. A branch applies a publication only after validating it and recording its applied version. A partially applied publication must not be reported as fully applied.

A branch must not locally edit centrally owned data. During an Offline period it uses its latest approved local snapshot within the permitted policy. An expired promotion ceases to apply and the approved base price is used.

### 5.2 Branch to Central

Examples include:

- Completed POS invoices and sale snapshots.
- POS tender outcomes and locally executed permitted refunds/returns.
- Stock movements, receiving confirmations, stocktakes, adjustments, and transfer execution evidence.
- Fulfillment picking/packing results and delivery attempt evidence.
- Branch expense and maintenance requests/evidence.
- Branch audit events and operational status/heartbeat.
- Synchronization health, checkpoint, and conflict records.

Messages represent facts that occurred at the branch. Central may validate, reconcile, approve, or flag them, but must not rewrite the historical source event. Corrections are represented by explicit compensating or adjustment operations with traceable references.

## 6. Ordering, batching, and checkpoints

- Messages are processed in bounded batches to avoid oversized requests and long-running transactions.
- The sender records the last confirmed acknowledgement/checkpoint per direction and contract stream.
- A retry resumes from the last confirmed checkpoint; it does not assume that a timed-out batch was wholly rejected.
- Ordering is guaranteed only within explicitly defined streams/aggregate keys where business rules require it. Independent aggregates may progress separately.
- A message that depends on another operation must declare its dependency/correlation and must not be applied before required prerequisites.
- A failed or conflicting message must not block unrelated messages that have no dependency on it.
- Batch size, maximum payload, timeout, concurrency, and throughput limits are operational tuning values to be established and load-tested before production.

## 7. Failure handling and retries

Classify outcomes explicitly:

| Outcome | Handling |
|---|---|
| Network unavailable / timeout / transient server error | Keep pending and retry with bounded exponential backoff and jitter |
| Receiver already processed the OperationId | Return the previously recorded result; do not repeat effects |
| Validation/schema error | Quarantine as a non-retryable contract error; record diagnostic and alert |
| Authorization/branch-scope failure | Reject and raise a security/operational alert; do not retry blindly |
| Business conflict | Record conflict case for Central review; continue unrelated operations |
| Partial batch success | Persist each operation outcome and retry only unacknowledged operations |
| Central or Branch service restart | Recover from durable Outbox/Inbox/checkpoints; no in-memory state is authoritative |

Exact retry intervals, maximum backoff, alert thresholds, and dead-letter retention are deployment/operations settings, not business policy. They must be configured, monitored, and tested; they must not be confused with the single customer delivery attempt.

## 8. Conflict handling

Conflicts must be explicit, traceable, and scoped to the affected operation.

Examples include:

- A branch event references an unknown or inactive product.
- A stock movement violates a required sequence or balance invariant.
- A centrally assigned reservation conflicts with branch stock already committed to a sale.
- A branch sends an obsolete or unauthorized master-data change.
- A message references an order, transfer, or approval that is not in a valid state.

For each conflict:

1. Persist the original message and its OperationId.
2. Record conflict category, affected aggregate/entity, source branch, timestamps, and diagnostic details.
3. Mark the operation as requiring review; do not silently overwrite either side.
4. Continue processing unrelated valid operations.
5. Central Administration reviews and records an explicit resolution/compensating action.
6. Publish the resolution to the branch when required.
7. Keep the original event and resolution history for audit.

Conflict resolution must respect the data ownership matrix in TD-04. Central cannot casually replace a branch's historical invoice or physical stock event; corrections must be explicit and auditable.

## 9. Offline policy and resumption gate

- Central Administration configures the maximum Offline duration. The configured duration is distributed to all branches and applies uniformly.
- The branch records the last successful Central verification and its current policy version/validity.
- Local login validity remains limited to 24 hours after the last successful Central verification, as approved.
- If the centrally configured Offline duration expires, the branch stops new sales and invoice issuance.
- Restoring network connectivity does not automatically resume POS sales.
- On reconnection, synchronization resumes from durable checkpoints.
- Before sales can resume, the system must:
  1. Complete all required synchronization operations or explicitly account for each remaining operation.
  2. Resolve blocking conflicts and failures through recorded outcomes.
  3. Complete required reconciliation of sales, tenders, stock, reservations, and centrally governed snapshots.
  4. Apply the latest approved central data and policy versions.
  5. Record Central Administration's explicit authorization to resume.
- The resume authorization is branch-specific, time-stamped, attributable to an authorized central actor, and audited.
- A branch must show a clear POS blocked/resynchronization state until authorization is received.

This resumption gate implements the approved rule; it does not create an additional customer-facing objection period or change the delivery policy.

## 10. Online order and stock-reservation synchronization

- Central owns online order and payment lifecycle state.
- The assigned branch receives an order/fulfillment instruction with a stable order/reservation reference and the required operational details.
- Branch acknowledgement records whether the instruction was durably accepted for fulfillment.
- POS must exclude stock reserved for website orders until Central releases the reservation or the branch records the approved release outcome.
- A branch outage prevents new website orders from being assigned to that branch.
- Picking and fulfillment results are sent to Central as branch facts.
- The single delivery attempt policy is enforced by the order/fulfillment workflow. A technical retry of a synchronization message is not another customer delivery attempt.
- Reservation expiry, late payment, branch acknowledgement timeout, and stock freshness thresholds must follow the approved BRS and be completed in the detailed order/API contract; this document does not invent new durations.

## 11. Security and observability requirements

- Authenticate every branch-to-central synchronization client and identify its registered BranchId/device/service identity.
- Authorize message types by source, target, branch scope, and operation.
- Protect traffic with HTTPS and managed secrets/certificates; exact mechanisms are specified in the security design.
- Do not include credentials, payment secrets, or unnecessary customer personal data in messages or logs.
- Log MessageId, OperationId, CorrelationId, branch, message type, schema version, processing result, duration, and failure category.
- Provide operational visibility for queue depth/age, last successful sync, last checkpoint, retry counts, conflicts, quarantined messages, and policy expiry.
- Alert on prolonged synchronization failure, growing queue age, repeated authorization failures, unresolved blocking conflicts, and approaching Offline expiry.
- Monitoring must not expose customer or payment data to unauthorized branch users.

## 12. Acceptance criteria for the technical design

The eventual implementation must demonstrate, through automated and integration tests, that:

1. A committed branch sale remains in the local Outbox after internet loss.
2. Reconnection delivers the sale without creating a duplicate central invoice or stock movement.
3. Replaying the same OperationId does not repeat any financial or inventory side effect.
4. A receiver crash after commit but before acknowledgement is safely recovered by retry.
5. A partially successful batch retries only unacknowledged operations.
6. One conflict is isolated while unrelated valid messages continue.
7. A centrally published price/promotion version is applied atomically and its version is recorded.
8. An expired promotion is not applied after its validity ends.
9. The centrally configured Offline duration is enforced consistently across branches.
10. Connectivity restoration alone does not unlock POS; explicit central resume authorization is required after reconciliation.
11. A synchronization retry does not create a second customer delivery attempt.
12. All message processing and conflict resolutions are traceable to source branch, actor/service identity, and operation IDs.

## 13. Decisions deferred to API and operations design

- Synchronization endpoint routes and request/response DTOs.
- Transport authentication mechanism, certificate/key rotation, and network topology.
- Exact batch sizes, payload limits, timeout, worker concurrency, and polling/trigger strategy.
- Retry backoff parameters, dead-letter retention, and alert thresholds.
- Message schema registry/version compatibility policy.
- Per-stream ordering and dependency model.
- Reservation acknowledgement timeout and stock freshness threshold.
- Operational dashboard and monitoring product.
- Data retention periods and log redaction details.

## 14. Approval boundary

TD-05 establishes the logical synchronization contract and safety invariants. It does not finalize transport endpoints, exact retry timings, SQL schema, authentication implementation, hosting, or authorize application coding.
