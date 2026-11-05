# Technical Design — API Boundaries and External Integrations

**Document:** TD-07  
**Version:** 0.1  
**Status:** Approved by project owner — baseline accepted for detailed design  
**Depends on:** Approved BRS, TD-01 through TD-06.

## 1. Purpose and principles

This document defines API boundaries between the Angular applications, Branch APIs, Central APIs, background workers, and approved external providers.

- Angular applications communicate only with application APIs, never directly with SQL databases.
- POS uses Branch.Api for branch operations and does not require Central availability for each sale.
- Store and customer workflows use Central.Api.
- Branch.Worker initiates synchronization with Central using the TD-05 contract.
- APIs enforce authentication, authorization, branch scope, validation, and workflow rules server-side.
- Retriable commands with financial, stock, order, or approval effects require idempotency.
- APIs expose business capabilities, not database tables or ORM entities.
- External providers are accessed through backend adapters; provider credentials never enter Angular bundles.
- API contracts are versioned and documented.
- No API design decision overrides an approved BRS rule.

## 2. Application-to-API boundaries

| Client / caller | API boundary | Responsibility |
|---|---|---|
| supermarket-pos | Branch.Api | POS sales, shifts, local stock, returns and branch tasks |
| supermarket-store | Central.Api | Customer account, catalogue, cart, checkout, online orders and payment status |
| supermarket-admin | Central.Api and explicitly authorized Branch.Api capabilities | Central governance, reports, approvals and branch administration |
| Branch.Worker | Central synchronization API | Durable branch-to-central and central-to-branch synchronization |
| Central.Worker | Internal services and provider adapters | Publication, webhooks, reconciliation and notifications |
| Payment provider | Central payment adapter | Payment status, refunds and verified callbacks |
| OTP/notification provider | Central notification adapter | OTP and approved notifications |

Admin may call branch-scoped capabilities only through authenticated APIs. It must never connect directly to a branch database.

## 3. Logical API ownership

### 3.1 Branch.Api

Branch.Api owns branch-local operations:

- Local employee authentication and authorization snapshot validation.
- POS device and cashier/shift context.
- Reads of locally approved product, unit, price and promotion snapshots.
- Sale validation, completion and invoice retrieval.
- Local tender recording for supported payment methods.
- Return inspection and authorized branch return actions.
- Stock availability, receiving, movement, stocktake and adjustment workflows.
- Fulfillment tasks assigned to the branch for online orders.
- Branch expense and maintenance workflows.
- Local operational status and synchronization diagnostics.

Branch.Api commits branch-owned business changes and Outbox records within the required local transaction boundary.

### 3.2 Central.Api

Central.Api owns centrally governed operations:

- Customer registration, OTP verification, sign-in and account recovery.
- Published catalogue, product information, base prices and promotions.
- Online cart, checkout, order lifecycle and stock reservation coordination.
- Online payment initiation, status, refund orchestration and verified provider callbacks.
- Employee, role, permission, branch and POS-device governance.
- Central approvals, loyalty policy, complaints and customer-service capabilities.
- Authorized central reporting and audit access.
- Synchronization endpoints for registered Branch Worker identities.

Central coordinates cross-boundary workflows through durable messages and explicit state transitions, not distributed SQL transactions.

## 4. API contract conventions

Proposed conventions, to be finalized in the detailed endpoint specification:

- Initial stable application API base path: /api/v1.
- JSON DTO contracts; do not expose ORM entities.
- UTC timestamps in ISO 8601 format.
- Opaque stable identifiers; avoid exposing sequential database keys unnecessarily.
- Monetary values use decimal amounts with explicit currency code and approved JOD rounding.
- Consistent pagination with bounded page sizes.
- Consistent machine-readable validation and error responses.
- Every request receives or propagates a CorrelationId.
- Retriable commands accept a stable OperationId/idempotency key.
- Responses never expose stack traces, secrets, database details or unnecessary personal data.
- Server-side authorization and validation are mandatory; hiding frontend controls is not security.

### 4.1 Common error categories

| Category | Meaning |
|---|---|
| Validation | Malformed request or invalid input |
| Unauthenticated | Missing, invalid or expired identity |
| Forbidden | Caller lacks permission or scope |
| Not found | Resource unavailable within authorized scope |
| Conflict | Workflow state prevents the requested transition |
| Rate limited | Configured request limit exceeded |
| Unavailable | Required service temporarily unavailable |
| Idempotent replay | Operation already completed; return recorded result |

Exact HTTP status mapping and error schema belong in the API specification. A successful HTTP response must not falsely imply a business operation is durably complete.

## 5. Initial API groups

### Branch API

- auth — local employee sign-in and session validation.
- devices — POS device identity and approved registration snapshot.
- shifts — cashier shift and drawer lifecycle.
- catalog — local approved product, unit and price snapshot reads.
- sales — sale validation, completion and invoice retrieval.
- tenders — tender recording and status.
- returns — return inspection and authorized return workflow.
- inventory — stock lookup, receipts, movements, counts and adjustments.
- fulfillment — assigned web-order picking, packing and delivery evidence.
- expenses — branch expense submission and status.
- maintenance — branch maintenance requests and status.
- sync-status — last sync, queue health, conflicts and Offline policy status.

### Central API

- customer-auth — registration, OTP verification, sign-in and recovery.
- customers — customer profile and privacy-scoped self-service.
- catalog — published catalogue and product details.
- pricing — centrally approved prices and promotions.
- cart — customer cart operations and validation.
- checkout — order placement and reservation initiation.
- orders — customer order view and central order lifecycle.
- payments — payment initiation, status and refund orchestration.
- payment-webhooks — provider callback intake and verification.
- employees — employee lifecycle and branch assignment.
- roles-permissions — role and permission governance.
- branches — branch configuration and status.
- pos-devices — device enrollment, authorization and revocation.
- approvals — authorized central approvals and conflict decisions.
- loyalty — centrally governed loyalty operations.
- complaints — customer complaint submission and case management.
- reports — authorized central reporting.
- sync — branch authentication, upload, download, acknowledgement and checkpoints.

These are logical groups, not separate deployable microservices. The solution remains a Modular Monolith unless an architecture change is separately approved.

## 6. Critical workflow contracts

### 6.1 POS sale

1. POS reads locally approved product, price and stock data from Branch.Api.
2. POS submits a sale command with cashier, shift, device, lines, quantities, tender details and stable OperationId.
3. Branch.Api validates authorization, active shift, local price validity, stock, reservations and sale rules.
4. Branch.Api commits invoice, stock movements, tender records, audit records and Outbox messages within the required local transaction boundaries.
5. The API returns the durable local invoice result and invoice number.
6. Central synchronization happens asynchronously through TD-05.

Retrying with the same OperationId returns the original outcome and must not create a second invoice.

### 6.2 Online checkout and payment

1. Store asks Central.Api to validate cart, current catalogue/prices, customer eligibility and fulfillment branch availability.
2. Central creates the order/reservation workflow and the approved 15-minute payment reservation window.
3. Central initiates payment through a backend payment adapter.
4. The customer uses the provider-approved hosted or tokenized payment experience.
5. Provider callbacks are verified server-side; a browser return URL is not proof of payment.
6. Central records verified payment status and confirms or releases the reservation according to the BRS.
7. The selected branch receives a fulfillment instruction.
8. Branch fulfillment facts are reported to Central through synchronization.

Duplicate checkout/payment requests must not create duplicate orders, charges or reservations.

### 6.3 Returns and refunds

- Branch return inspection and permitted local actions use Branch.Api.
- Central payment refund orchestration uses Central.Api and the provider adapter.
- Refunds reference the original invoice/order and payment transaction.
- Refund the original payment method where possible; COD follows the approved cash refund rule.
- Refund requests have stable OperationIds and explicit pending, succeeded, failed or reconciliation states.
- A provider timeout is not proof of success or failure. Reconcile provider state before issuing another financial operation.

### 6.4 Branch synchronization

- Branch.Worker authenticates and uploads bounded Outbox batches.
- Central validates branch identity, message contract, authorization, idempotency and dependencies.
- Central returns durable per-operation outcomes and checkpoints.
- Branch.Worker downloads centrally published data and applies it transactionally to local snapshots.
- Central never connects directly to branch databases.
- Synchronization retry follows TD-05 and is not a second customer delivery attempt.

## 7. External integration boundaries

External providers are wrapped behind backend interfaces/adapters so provider-specific behavior does not leak into business modules.

| Integration | Owning side | Required behavior |
|---|---|---|
| Electronic payment provider | Central | Hosted/tokenized payment, verified webhooks, idempotency and reconciliation |
| OTP/SMS provider | Central | OTP generated and verified internally; provider only delivers message |
| Email/notification provider | Central | Template-based notification, delivery status and controlled retry |
| Tax/e-invoicing authority | Central, future placeholder | No active implementation until legal scope is approved |
| Monitoring/telemetry | Central and Branch | Redacted operational metrics and diagnostics |

Provider selection, contracts, credentials, sandbox accounts, callback URLs and certification remain open decisions. No provider is selected by this document.

## 8. Webhooks and callbacks

- Receive provider callbacks only through dedicated backend endpoints.
- Verify provider authenticity/signature, timestamp/replay controls, merchant account and event schema.
- Persist callback receipt before acknowledging it.
- Deduplicate provider event IDs and map them to internal OperationIds.
- Never trust browser redirects or client-supplied payment status.
- Process callbacks asynchronously when needed, using durable Inbox/Outbox patterns.
- Reconcile ambiguous payment states with provider query APIs; do not blindly create another charge or refund.
- Log event identifiers and outcomes, never card data, secrets, OTPs or full sensitive payloads.

## 9. API security

TD-06 applies to every API. Additionally:

- Every endpoint declares required identity type, permission, branch scope and resource constraints.
- Synchronization endpoints accept only registered branch service identities and authorized message types.
- Customer endpoints enforce resource ownership and prevent IDOR.
- Admin endpoints require explicit central permissions and audit high-impact actions.
- Payment webhook endpoints use provider verification independent of employee/customer bearer tokens.
- Apply rate limits to login, OTP, cart/checkout, payment initiation and public catalogue endpoints.
- Validate request size, content type, schema version and allowed fields.
- Use allowlisted CORS origins for known Angular applications.
- Keep provider credentials and signing keys server-side.
- Apply CSRF protections if cookie-based sessions are selected.
- Document externally reachable endpoints and review for unintended data exposure.

## 10. Versioning and compatibility

- Start with /api/v1.
- A change is breaking if it removes/renames fields, changes semantics or state transitions, or incompatibly changes authorization requirements.
- Prefer additive optional fields for compatible evolution.
- Version synchronization message schemas independently from HTTP route versioning, as defined in TD-05.
- Central must support the agreed compatibility window while branches are upgraded at different times.
- Unsupported schema versions are rejected with a clear durable outcome and operational alert.
- Maintain contract tests between Branch.Worker and Central synchronization APIs.
- The supported compatibility window is a release policy decision before staged rollout.

## 11. Observability

Record with sensitive data redacted:

- CorrelationId, OperationId and MessageId where relevant.
- API operation, caller identity category, BranchId and DeviceId where relevant.
- Result category, status, duration, retry count and provider event reference.
- Validation/conflict category without full customer payloads or secrets.

Monitor API availability/latency, authorization failures, payment callbacks and reconciliation, checkout/reservation failures, synchronization lag, Offline policy expiry and provider outages.

## 12. API acceptance criteria

The detailed API specification and implementation must demonstrate that:

1. Angular applications never access SQL databases directly.
2. POS sales complete locally through Branch.Api without Central availability, within approved Offline limits.
3. Repeated sale requests with the same OperationId return the same invoice outcome.
4. Store checkout cannot confirm payment based only on browser redirect.
5. Payment webhook replay cannot create duplicate payment or refund effects.
6. A customer cannot read another customer's orders by changing an identifier.
7. Branch A cannot submit or retrieve Branch B data using its service identity.
8. Unauthorized callers cannot invoke manager/central approval operations.
9. Reserved website stock cannot be sold by POS until the approved release outcome.
10. Branch fulfillment evidence does not change the single delivery attempt policy.
11. API/schema incompatibility is detected and reported explicitly.
12. Provider timeout does not silently duplicate financial operations.
13. Secrets and sensitive personal/payment data are absent from logs and client bundles.
14. High-impact API commands are attributable and auditable.

## 13. Decisions deferred to detailed API specification

1. Exact endpoint routes, HTTP verbs, DTO fields and examples.
2. OpenAPI publication and client generation strategy.
3. Payment provider and hosted/tokenized integration method.
4. OTP/SMS and email provider selection.
5. Authentication/session/token protocol and browser storage strategy.
6. Payment callback signature scheme and provider reconciliation schedule.
7. Rate-limit values, request limits, timeouts and retry policies.
8. API compatibility window and branch rollout strategy.
9. Tax/e-invoicing provider and legal scope, if activated.
10. API gateway/reverse proxy and network exposure topology.

## 14. Approval boundary

TD-07 establishes logical API ownership, workflows, security expectations and integration boundaries. It does not finalize individual endpoint DTOs, choose external providers, configure hosting/network infrastructure, or authorize application coding.
