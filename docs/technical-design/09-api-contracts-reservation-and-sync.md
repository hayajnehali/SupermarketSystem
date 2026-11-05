# TD-09 — Detailed API Contracts: Online Reservation and Branch Synchronization

**Version:** 0.1  
**Status:** Proposed for owner review  
**Scope:** Technical contract detail for the approved online reservation, payment coordination, and Branch.Worker command-polling design.  
**Depends on:** Approved BRS and TD-01 through TD-08, especially TD-04, TD-05, TD-06, and TD-07.

## 1. Contract rules

- All routes use the existing /api/v1 version prefix and JSON DTOs.
- Timestamps are UTC ISO 8601. Identifiers are opaque; monetary values use decimal amounts with explicit currency.
- Every request/response carries or propagates a CorrelationId.
- Commands with stock, payment, order, or financial effects require a stable OperationId or idempotency key. Replays return the original durable outcome and do not repeat effects.
- Transport delivery is at-least-once. Business effects are idempotent through durable operation receipts.
- A successful HTTP response means only what the endpoint contract explicitly says. Command delivery is not proof that a Branch business operation committed.
- Branch DB remains authoritative for local stock reservation and POS sale serialization. No endpoint permits Central to access a Branch database directly.
- Authentication and branch scoping follow TD-06. A Branch service identity acts only for its registered BranchId.
- DTOs below are logical contract shapes. Exact serialization casing, common error envelope, generated OpenAPI artifacts, and provider-specific fields are finalized during implementation contract generation.

## 2. Common response and error envelope

Successful responses include CorrelationId, OperationId where relevant, status, and endpoint-specific data.

Errors include:
- code: stable machine-readable code
- category: Validation, Unauthenticated, Forbidden, NotFound, Conflict, RateLimited, or Unavailable
- message: safe human-readable summary
- correlationId
- details: optional field/reason details without secrets or stack traces
- retryable: whether the same operation may be retried

HTTP mapping:
- 200: request completed and a recorded result is returned.
- 201: a new resource was durably created.
- 202: accepted for asynchronous processing; not a claim of business completion.
- 400: malformed or invalid request.
- 401 / 403: authentication or authorization failure.
- 404: resource not found within caller's permitted scope.
- 409: state conflict or idempotency-key reuse with a different request body.
- 422: well-formed request rejected by durable business validation.
- 429: rate limit.
- 5xx: temporary service/transport failure; retry only with the same operation identity.

## 3. Customer checkout and reservation coordination

### 3.1 Create reservation intent

POST /api/v1/checkout/reservations

Caller: authenticated customer through Central.Api.

Headers:
- Idempotency-Key: stable client-generated key for this checkout attempt.
- X-Correlation-Id: optional correlation identifier.

Request shape:

    {
      "operationId": "opaque-id",
      "cartVersion": "opaque-version",
      "items": [
        {
          "productId": "opaque-id",
          "unitId": "opaque-id",
          "quantity": 2
        }
      ]
    }

Contract:
- Central validates the customer/cart and performs the already-approved branch-selection workflow.
- Central creates one durable reservation intent and one stable Branch reservation command.
- Central returns 202 Accepted while Branch acceptance is pending. It must not create/start a payment session or claim stock is reserved at this point.
- Replay with the same key and identical normalized request returns the same reservation resource. Reuse with a different payload returns 409 IdempotencyKeyPayloadMismatch.

Response shape:

    {
      "reservationId": "opaque-id",
      "operationId": "opaque-id",
      "status": "AwaitingBranchReservation",
      "correlationId": "opaque-id"
    }

### 3.2 Read reservation workflow status

GET /api/v1/checkout/reservations/{reservationId}

Caller: owning customer or authorized Central service/operator.

Returns the current customer-safe reservation/order workflow state and, only when applicable, payment-session information. It does not expose internal Branch credentials, command payloads, or sensitive provider data.

The response distinguishes:
- AwaitingBranchReservation
- AwaitingPayment
- PaymentVerificationPending
- PaymentSucceededAwaitingBranchCommit
- Confirmed
- ReleasePending
- Released
- ReservationRejected
- ReservationException
- PaymentException
- ReleaseException

The API returns the state persisted by Central; it never infers Branch acceptance from a send attempt or timeout.

## 4. Central-to-Branch command polling

### 4.1 Pull commands

POST /api/v1/sync/commands:pull

Caller: Branch.Worker using its registered machine identity.

Request shape:

    {
      "branchId": "registered-branch-id",
      "workerInstanceId": "opaque-id",
      "maxItems": 20,
      "waitSeconds": 20,
      "knownCheckpoint": "opaque-cursor"
    }

Contract:
- Central derives the authorized BranchId from the authenticated service identity and verifies that the body BranchId matches it.
- maxItems and waitSeconds are bounded by server configuration.
- Commands are returned in a bounded batch, ordered by Central's durable enqueue order within the branch queue.
- Delivery may repeat. Each command includes stable commandId, operationId, reservationId when applicable, commandType, schemaVersion, createdAtUtc, and payload.
- A command lease/visibility timeout is operational configuration. Lease expiry may make the same command available again; it never changes the business reservation/payment deadline.
- Empty queue returns 200 with an empty commands array and current checkpoint.

Response shape:

    {
      "branchId": "registered-branch-id",
      "commands": [
        {
          "commandId": "opaque-id",
          "operationId": "opaque-id",
          "reservationId": "opaque-id",
          "commandType": "ReserveStock",
          "schemaVersion": 1,
          "createdAtUtc": "2026-11-06T10:00:00Z",
          "payload": {}
        }
      ],
      "nextCheckpoint": "opaque-cursor",
      "correlationId": "opaque-id"
    }

### 4.2 Supported command types

Initial command catalogue:
- ReserveStock: atomically validate sellable stock and persist a reservation in Branch DB.
- CommitReservationToOrder: associate an existing reserved quantity with the confirmed order workflow.
- ReleaseReservation: release the exact reservation identity and quantity.

Every command is validated against BranchId, reservation identity, schema version, and legal local state transition. Unsupported schema versions are rejected safely and surfaced for compatibility handling.

### 4.3 Acknowledge command outcome

POST /api/v1/sync/commands/{commandId}/outcome

Caller: authenticated Branch.Worker.

Request shape:

    {
      "operationId": "opaque-id",
      "branchId": "registered-branch-id",
      "outcome": "Accepted",
      "reservationId": "opaque-id",
      "branchTransactionId": "opaque-id",
      "reasonCode": null,
      "processedAtUtc": "2026-11-06T10:00:01Z"
    }

Allowed outcomes:
- Accepted: reservation durably committed in Branch DB.
- Committed: reservation durably associated with the confirmed order.
- Released: exact reservation durably released in Branch DB.
- Rejected: durable business rejection; includes stable reasonCode.
- AlreadyProcessed: Branch returns the original recorded outcome for the same operation.
- RetryableFailure: no business effect committed; same operation may be retried.

Contract:
- Branch commits its stock change and processed-operation receipt in one local transaction before reporting an applied outcome.
- Central authenticates and verifies Branch scope, command identity, operation identity, and expected command type.
- Central records the outcome durably and applies only a legal state transition.
- Duplicate acknowledgements are idempotent and return the previously recorded Central result.
- An acknowledgement with mismatched identifiers or an impossible transition is rejected and audited; it must not overwrite a prior result.

Response shape:

    {
      "commandId": "opaque-id",
      "operationId": "opaque-id",
      "recorded": true,
      "centralWorkflowState": "AwaitingPayment",
      "correlationId": "opaque-id"
    }

centralWorkflowState is returned only when the acknowledgement causes a valid workflow transition. A Branch Accepted result for ReserveStock is the prerequisite for Central to create/start the payment session.

## 5. Branch-side transaction boundary

These operations execute inside the Branch application/database boundary, not as direct Central API writes.

### ReserveStock
1. Begin a database transaction.
2. Check current sellable quantity against existing active reservations and POS sale effects using the same serialization/concurrency boundary.
3. If sufficient, persist the reservation and processed-command receipt atomically.
4. Commit.
5. Only after commit, return Accepted.

If insufficient, persist the command receipt and Rejected outcome without reserving quantity.

### CommitReservationToOrder
1. Begin a transaction.
2. Verify the exact reservation identity is in Reserved state and belongs to the expected branch/order.
3. Transition it to CommittedToOrder and persist the processed-command receipt atomically.
4. Commit, then acknowledge Committed.

### ReleaseReservation
1. Begin a transaction.
2. Verify the exact reservation identity and legal releasable state.
3. Release no more than the quantity held by that reservation; persist Released and processed-command receipt atomically.
4. Commit, then acknowledge Released.
5. POS can sell the released quantity only after this local commit.

The database isolation/locking or conditional-update implementation is selected and load-tested during implementation. The invariant is mandatory: POS sale and web reservation cannot both consume the same units.

## 6. Payment coordination boundary

Payment provider endpoints remain behind Central.Api adapters; provider credentials never reach Angular or Branch clients.

- Central creates/starts a payment session only after durable Branch Accepted for ReserveStock.
- Payment initiation uses a stable merchant reference/idempotency identity tied to the reservation/payment attempt.
- Provider callbacks are authenticated, deduplicated by provider event/transaction identity, and checked against authoritative provider status before Central changes payment state.
- Verified payment success moves Central to PaymentSucceededAwaitingBranchCommit and creates one durable CommitReservationToOrder command.
- Central enters Confirmed only after Branch's durable Committed acknowledgement.
- Definitive payment failure/cancellation causes one durable ReleaseReservation command.
- Unknown provider outcome remains PaymentVerificationPending; it is not treated as failure and does not authorize stock release.
- If payment success conflicts with a Branch-confirmed release, Central records a payment exception for authorized reconciliation. This API specification does not choose refund or re-reservation policy.

## 7. Security and authorization requirements

| Endpoint group | Required caller scope |
|---|---|
| Customer checkout/status | Authenticated customer; resource ownership enforced |
| Command pull | Registered Branch.Worker identity; branch-scoped sync.commands.read |
| Command outcome | Registered Branch.Worker identity; branch-scoped sync.commands.ack |
| Payment initiation/status | Customer ownership for reads; Central internal payment service for provider operations |
| Provider webhook | Provider-specific signature/authentication and replay protection |

Additional requirements:
- Never trust a BranchId supplied only in the request body; compare it with authenticated identity.
- Apply request size limits, rate limits, schema validation, and audit logging.
- Do not log payment credentials, OTPs, access tokens, or full sensitive customer payloads.
- Use TLS for all cross-boundary traffic.
- Authentication key/credential format and rotation lifecycle follow the separate TD-06 security implementation decision.

## 8. Idempotency, concurrency, and observability

- Persist idempotency records with a unique constraint over the relevant caller/scope and operation identity.
- Store a request hash to detect accidental reuse of an idempotency key with a different payload.
- Persist Branch command receipts in the same transaction as the corresponding stock/reservation mutation.
- Persist Central command acknowledgements and workflow transitions atomically.
- Use CorrelationId, CommandId, OperationId, ReservationId, BranchId, and provider transaction/event identifiers for traceability.
- Record structured metrics for command age, poll latency, acknowledgement latency, retry count, rejected commands, pending payment verification, pending Branch commit, and pending release.
- Logs must be structured and redact sensitive values. Audit records for state changes are append-only.

## 9. Compatibility and versioning

- API route prefix begins at /api/v1.
- Every synchronization command carries schemaVersion.
- Central must not dispatch a command schema that the registered Branch version does not support.
- Branch reports supported command schema versions during authenticated worker registration/health reporting.
- Breaking contract changes require a new compatible schema/API version and a staged Central/Branch rollout plan.
- Retain backward compatibility for the documented deployment overlap window; exact duration is an operational release decision, not a business rule.

## 10. Explicitly deferred implementation details

Not selected by this document:
- Exact authentication credential format, certificates, key storage, and rotation ceremony.
- Poll cadence, maximum wait, batch size, command lease duration, and retry/backoff thresholds.
- Concrete SQL tables/indexes and exact locking/isolation strategy.
- Exact generated OpenAPI schema, C# DTO names, and source-code project placement.
- Provider-specific webhook signature algorithms and provider status mapping.
- Operational alert thresholds and runbook ownership.

These are detailed implementation/security/operations tasks. They do not change approved business rules or the logical API behavior above.
