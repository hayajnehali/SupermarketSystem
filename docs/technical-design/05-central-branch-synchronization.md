# Technical Design — Central/Branch Synchronization

**Document:** TD-05  
**Version:** 0.1  
**Status:** Approved by project owner — baseline accepted for detailed design  
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

**Owner-approved stock-concurrency invariant (2026-11-06):** The Branch database is the final authority for accepting a reservation against its physical stock. Applying a website reservation and validating a POS sale must serialize against the same local stock/reservation state. Branch must commit the reservation and its durable processing receipt before acknowledging acceptance to Central. Central must not interpret a transport send or an uncommitted response as proof that stock is reserved. Duplicate delivery of the same reservation OperationId returns the recorded outcome and must not reserve stock twice. The exact transport/coordination mechanism, acknowledgement timeout, and detailed failure-state contract remain open for the next design step.

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


## 15. Branch-initiated command delivery — owner-approved mechanism

**Owner approval: 2026-11-06.** The project owner approved outbound HTTPS polling initiated by `Branch.Worker` as the logical delivery mechanism for Central-to-Branch commands, including online stock reservations and releases. It preserves the approved rule that Branch owns the final stock decision and that Central has no direct database connection to a branch.

### 15.1 Connection direction

- `Branch.Worker` initiates outbound HTTPS requests to the Central API.
- Central does not open inbound connections to branch networks and does not connect to branch SQL databases.
- The Worker uses a configurable polling loop to request commands addressed to its registered BranchId.
- Poll interval, request timeout, batch size, and concurrency are operational configuration values; they are not fixed here.
- The Worker must use the existing authenticated service identity and branch-scoped authorization defined by TD-06. A request for one branch must never retrieve another branch's commands.

### 15.2 Command delivery lifecycle

1. Central commits a command to its durable command/Outbox store with a stable `CommandId`, `OperationId`, target `BranchId`, command type, schema version, and correlation metadata.
2. On its next poll, the Branch Worker requests a bounded batch of commands for its own branch. Central returns command identifiers and payloads; delivery is at-least-once.
3. The Worker passes each command to the appropriate Branch application handler. The handler validates the target branch, command version, business state, and current local data.
4. For a stock reservation, the Branch handler atomically checks sellable stock against existing reservations and POS sales, persists the reservation and the processed-command receipt in the same Branch database transaction, and only then returns an accepted outcome.
5. For a release, the Branch handler durably applies the release and records the processed-command receipt before returning a released outcome. POS may sell the released quantity only after this local commit.
6. The Worker posts each outcome to Central. Central records the acknowledgement against the same command/operation identity and updates its workflow state only from a valid, authenticated Branch outcome.
7. If the response or acknowledgement is lost, the Worker may deliver the same command again. The Branch returns its previously persisted outcome without repeating the stock effect.

### 15.3 Reliability and concurrency safeguards

- Central command creation must be durable before the command is offered to a Worker.
- Delivery is at-least-once; exactly-once transport is neither assumed nor required. Exactly-once business effect is achieved through durable idempotency at the Branch.
- The Branch database transaction is the serialization boundary shared by website reservation handling and POS sale validation.
- Central must not start the approved 15-minute payment window or present stock as reserved until it has received the Branch's durable reservation acceptance and created/started the payment session according to the payment workflow.
- A command that has not been durably accepted by the Branch is not a confirmed reservation.
- A timeout, lost response, or Worker restart must not cause Central to assume acceptance or create a second reservation/payment attempt. The same stable identifiers are reused during recovery.
- Central records command delivery status separately from business outcome. A successful HTTP response alone is not proof that the business operation succeeded.
- A command rejected for insufficient stock or invalid state returns a durable business rejection; transient transport failures remain eligible for retry.
- A release is idempotent. Repeated release delivery must not increase available stock more than once.
- If the Branch is unreachable during checkout, Central fails the reservation handshake safely; it must not proceed as though stock were reserved.

### 15.4 Minimum command outcomes

The Branch must return a durable outcome for each command, at minimum:
- `Accepted`: reservation was committed locally.
- `Released`: release was committed locally.
- `Rejected`: a permanent business validation prevented application, with a stable reason code.
- `AlreadyProcessed`: the same operation was previously processed; include the original outcome.
- `RetryableFailure`: no business effect was committed and the same operation may be retried.

The API contract will define exact DTOs, status codes, and reason-code catalogues later. A transport-level failure is not a business rejection.

### 15.5 Timing and policy boundaries

- This mechanism does not change the approved 15-minute website payment window. That window begins only after durable Branch reservation acceptance and initiation of the payment session; command polling/transport delay before acceptance does not consume it.
- It does not change the centrally configured Offline duration, local login validity, or the requirement for explicit Central authorization before POS resumes after an Offline period.
- Technical command retries do not count as another customer delivery attempt.
- No additional customer objection period or delivery attempt is introduced.
- Polling frequency, command lease/visibility timeout, retry backoff, and operational alert thresholds remain configurable implementation/operations details and must be finalized during API and deployment design.

### 15.6 Approval boundary

This mechanism is an approved logical baseline. It selects outbound `Branch.Worker` polling over Central-initiated inbound connections. Approval does not finalize endpoint paths, authentication credential format, polling interval, SQL table design, or payment-provider exception handling; those remain detailed API/security/operations work.


## 16. Online stock-reservation state machine — owner-approved baseline

**Owner approval: 2026-11-06.** The project owner approved this logical state model. It defines separate Central workflow states and Branch stock states. They must not be collapsed into one status because Central owns the online order/payment lifecycle while the Branch database owns the physical stock reservation.

### 16.1 Central reservation/order states

| State | Meaning | Allowed next states |
|---|---|---|
| `AwaitingBranchReservation` | Central has durably created the reservation command; Branch has not yet confirmed it | `AwaitingPayment`, `ReservationRejected`, `ReservationException` |
| `AwaitingPayment` | Branch has durably reserved the requested quantity and Central has initiated the payment session; the approved 15-minute window is running | `PaymentVerificationPending`, `Confirmed`, `ReleasePending` |
| `PaymentVerificationPending` | The 15-minute window elapsed or the payment result is uncertain; Central is obtaining an authoritative provider result | `Confirmed`, `ReleasePending`, `PaymentException` |
| `Confirmed` | Payment is authoritatively successful and the order is confirmed | Fulfillment states defined by the order workflow |
| `ReleasePending` | Central has decided the reservation must be released and has durably queued a release command | `Released`, `ReleaseException` |
| `Released` | Branch has durably applied the release and acknowledged it | Terminal for this reservation |
| `ReservationRejected` | Branch durably rejected the reservation, for example because sellable stock was insufficient | Terminal for this reservation attempt |
| `ReservationException` / `PaymentException` / `ReleaseException` | A blocking technical or business exception requires reconciliation; no success is assumed | Explicitly resolved transition only |

Central must not enter `AwaitingPayment` until it receives the Branch's durable `Accepted` outcome. A timeout before that acknowledgement leaves the workflow in `AwaitingBranchReservation` or `ReservationException`; it must not start the 15-minute payment window or claim stock is reserved.

### 16.2 Branch stock-reservation states

| State | Meaning |
|---|---|
| `Reserved` | Quantity is durably excluded from POS sellable stock |
| `CommittedToOrder` | Reservation has been associated with the confirmed order/fulfillment workflow; it remains unavailable to POS |
| `Released` | Reservation is durably released and its quantity becomes sellable subject to current stock rules |
| `Rejected` | Reservation command was processed but no quantity was reserved |

The Branch also stores the processed `OperationId` and original outcome. Repeated commands return that outcome and do not repeat stock changes. A release command must reference the exact reservation identity; it must never release an unrelated reservation or more quantity than that reservation holds.

### 16.3 Normal flow

1. Central creates the order/reservation intent and durable reservation command.
2. Branch receives the command and atomically checks stock against POS sales and all active reservations.
3. Branch commits either `Reserved` or `Rejected`, together with its processing receipt.
4. Only after `Reserved` is acknowledged does Central initiate the payment session and enter `AwaitingPayment`.
5. An authoritative successful payment result moves Central to `Confirmed`; Central sends the order-confirmation/commit instruction to the Branch, which moves the reservation to `CommittedToOrder`.
6. If payment is authoritatively failed/cancelled, or the payment window expires and the provider confirms no successful payment, Central enters `ReleasePending` and sends an idempotent release command.
7. Branch commits `Released`, acknowledges it, and only then may POS sell the released quantity. Central then enters `Released`.

### 16.4 Payment uncertainty and expiry safety

- The 15-minute payment window is a fixed approved business window; provider verification and webhook processing do not extend that window.
- Expiry of the 15-minute clock is not, by itself, proof that payment failed.
- If the provider outcome is still unknown at expiry, Central enters `PaymentVerificationPending`; it must not mark the order confirmed and must not release the Branch reservation while a successful payment could still be reported for that payment attempt.
- When the provider reports a definitive success, Central follows the confirmation path. When it reports a definitive failure/cancellation, Central follows the release path.
- If the provider cannot establish a definitive outcome or contradictory callbacks are received, the case enters `PaymentException` for reconciliation. No duplicate payment attempt or automatic stock release is inferred by this design.
- This rule preserves the existing pending-verification/webhook approach and does not add a new payment window, customer objection period, or delivery attempt.

### 16.5 Failure and retry rules

- A lost reservation acknowledgement is resolved by querying/replaying the same `OperationId`; Central must not create a second reservation identity.
- A Branch rejection is a business outcome, not a transport retry.
- A transport failure before Branch commit may be retried with the same operation identity.
- A release timeout leaves Central in `ReleasePending`; POS must continue excluding the quantity until Branch confirms `Released`.
- If Branch committed an operation but its acknowledgement was lost, replay returns the stored result.
- Invalid state transitions are rejected and audited. Manual reconciliation must create an explicit, attributable resolution; it must not overwrite historical outcomes.

### 16.6 Approval boundary

This state model is approved as the logical baseline. It defines logical states and safety transitions only. Detailed order statuses, payment-provider mapping, webhook contracts, timeout handling, and exception-resolution permissions will be specified in the API and payment integration design. No additional customer-facing policy is introduced here.


## 17. Reservation/payment exception handling — proposed operational rules

This section defines safe technical handling for failures and race conditions. It does not create new customer-facing refund, cancellation, delivery, or objection policies.

### 17.1 Important state clarification

A successful payment-provider result and a confirmed order are separate facts. To avoid Central showing an order as confirmed before the Branch has durably associated the reservation with that order, add the Central state `PaymentSucceededAwaitingBranchCommit`:

- Enter it only after the provider's authoritative successful payment result is verified.
- Central durably records the payment result and queues an idempotent Branch commit command.
- The Branch moves `Reserved` to `CommittedToOrder` in a local transaction and acknowledges the same reservation/operation identity.
- Central enters `Confirmed` only after receiving the Branch's durable commit acknowledgement.
- If the Branch is unavailable, remain in `PaymentSucceededAwaitingBranchCommit`, retry/query using the same operation identity, and raise an operational alert when configured thresholds are exceeded. Do not initiate another charge and do not release the reservation.

This clarification does not change the approved payment window or payment success criteria; it makes the meaning of order confirmation consistent across Central and Branch.

### 17.2 Exception matrix

| Scenario | Required safe behavior | Recovery condition |
|---|---|---|
| Reservation command not yet received by Branch | Keep Central in `AwaitingBranchReservation`; retry delivery using the same `CommandId`/`OperationId` | Durable Branch outcome received |
| Branch committed reservation but acknowledgement was lost | Replay/query the same operation; Branch returns its stored outcome without reserving again | Central records original `Accepted` or `Rejected` result |
| Branch rejects for insufficient sellable stock | Mark the reservation attempt rejected; do not start payment | Customer may follow the normal site flow to revise/retry the cart; no automatic alternate-branch assignment is introduced here |
| Payment-session creation request times out | Treat provider result as unknown; query by the same merchant/payment reference or idempotency key; do not create a second payment session/charge blindly | Provider confirms created/failed/no session |
| Payment window expires with provider status unknown | Enter `PaymentVerificationPending`; keep reservation unavailable to POS; verify with provider/webhook reconciliation | Authoritative provider success or definitive failure/cancellation |
| Provider reports payment success more than once | Deduplicate provider event and payment identity; apply the success transition once | Existing recorded success is returned for duplicates |
| Provider reports definitive failure/cancellation | Queue one idempotent Branch release command; keep quantity unavailable to POS until Branch acknowledges release | Branch durably records `Released` |
| Release command or acknowledgement is lost | Retry/query the same release operation; never release twice | Branch's durable `Released` outcome is received |
| Payment succeeds while Branch is offline after payment | Record `PaymentSucceededAwaitingBranchCommit`; keep reservation held locally and retry commit delivery; do not confirm the order until Branch acknowledges | Branch commit acknowledgement |
| Payment success arrives after Branch has already confirmed release | Enter a critical `PaymentException`; block automatic order confirmation and automatic stock adjustment. Preserve payment and release evidence for authorized reconciliation | Explicit audited resolution under an owner-approved payment exception policy |
| Duplicate or out-of-order webhooks | Verify current payment status with the provider; deduplicate event IDs and enforce legal state transitions; stale events cannot reverse a terminal verified outcome | Provider's authoritative status and idempotent event processing |
| Branch reports a conflicting outcome for the same operation | Quarantine the workflow in an exception state; do not guess which stock/payment result is correct | Reconciliation using durable Branch ledger, Central command history, and provider transaction evidence |

### 17.3 Invariants for every recovery path

- Never mark an order `Confirmed` solely because a payment webhook arrived; verify the payment and obtain the Branch commit acknowledgement.
- Never allow POS to sell reserved stock because Central timed out or because a release command was merely queued.
- Never create a new reservation/payment/release identity to recover a lost acknowledgement; reuse the original idempotency identity for the same business operation.
- Never interpret a transport timeout as a business failure.
- Provider events are authenticated, deduplicated, and checked against the provider's authoritative transaction status.
- Every manual resolution records actor, timestamp, evidence/reason, previous state, and resulting state. No historical transaction or acknowledgement is overwritten.
- Alerts and retry schedules are operational configuration; they do not alter the approved customer-facing 15-minute payment window.

### 17.4 Decisions deliberately not made here

The following require a separate explicit business decision and are not inferred by this technical design:
- Whether a successfully paid order whose stock reservation was already released should be refunded automatically, manually refunded, or re-reserved if stock is available.
- The customer communication and service-level target while payment or Branch commit reconciliation is pending.
- The exact role permitted to approve financial exception resolution and any monetary threshold.
- The exact retry/alert thresholds and provider-specific status mapping.

### 17.5 Approval boundary

This exception-handling model and the `PaymentSucceededAwaitingBranchCommit` clarification are proposed for owner approval. Once approved, they become the logical technical baseline; detailed API contracts, provider-specific integration, alert thresholds, and operational runbooks remain implementation design work.
