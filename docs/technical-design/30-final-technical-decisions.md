# TD-30 — Final Technical Decisions: Offline Identity, Concurrency, Promotions, Supplier Payments, Rounding and Retention

**Status:** Approved by owner (2026-11-28)  
**Depends on:** TD-12 through TD-29 and the approved business/technical baseline.  
**Purpose:** Record the eight remaining technical decisions approved by the owner. This document closes the corresponding decision gates without authorizing implementation, physical schema changes, DDL, EF Core entities or migrations.

## 1. Decision register

| # | Decision | Approved choice | Status |
|---|---|---|---|
| 1 | Offline Identity / Session Persistence | A + B | Approved |
| 2 | Stock Concurrency | B | Approved |
| 3 | Refund Concurrency | B | Approved |
| 4 | Offline Promotions | A | Approved |
| 5 | Supplier Payments | B | Approved |
| 6 | Rounding | C | Approved; exact midpoint tie-breaker remains a narrow implementation detail |
| 7 | Promotion Rules | C | Approved |
| 8 | Data Retention | C | Approved |

## 2. Detailed decisions

### TD-30.1 — Offline Identity / Session Persistence

**Approved policy:**
- A Branch may authenticate an employee locally using a protected local session/credential-verifier record. The Branch must not copy Central password hashes, reusable Central tokens or security stamps.
- Local employee authentication is permitted for up to 24 hours from the last successful verification with Central, consistent with the previously approved Offline limit.
- During disconnection, the Branch uses the last successfully synchronized employee status and permission snapshot.
- On reconnection, employee status and permissions must be refreshed from Central. Sensitive operations remain restricted until this refresh and verification complete.
- If Central reports that an account is disabled or permissions have changed, the Branch applies the updated status/permissions before restoring normal operation.

**Security implementation remains required:** cryptographic construction, key protection, local verifier lifecycle, revocation propagation, and secure storage must be specified and tested before implementation. This decision does not approve storing Central credentials locally.

### TD-30.2 — Stock Concurrency

**Approved choice: Optimistic Concurrency (B).**
- Use a concurrency token such as SQL Server `rowversion` for mutable stock/balance records where concurrent updates must be detected.
- Stock issue/reservation must also enforce the available-quantity condition atomically in the database; a successful concurrency check alone is not permission to make stock negative.
- On a conflict, reject/reload/re-evaluate the operation; retry only when the business operation is safe and idempotent.
- Batch allocation and stock ledger effects must be committed atomically with the sale/reservation and its durable Outbox operation.
- Do not hold long-lived pessimistic locks across user interaction or network calls.

### TD-30.3 — Refund Concurrency

**Approved choice: Optimistic Concurrency (B).**
- Use concurrency detection for the invoice/payment refund state.
- Within one database transaction, re-read or atomically validate the successful refunded amount against the original paid amount, then record the refund and its cash-drawer transaction.
- The cumulative successful refund must never exceed the original amount paid; concurrent requests must not both consume the same remaining refundable balance.
- A conflict is rejected and re-evaluated. Idempotency keys/OperationId prevent duplicate processing of the same request.
- Preserve the already approved cash-only customer refund policy and Central approval requirement for refunds involving original electronic tender.

### TD-30.4 — Offline Promotions

**Approved choice: A — use locally stored approved promotions only while valid.**
- Branches keep versioned snapshots of Central-approved promotion definitions.
- A promotion may be applied Offline only when the local snapshot is approved, complete, and within its configured validity period.
- An expired promotion is not applied, even if the Branch remains disconnected.
- Branches cannot create or edit promotions locally.
- On reconnection, usage is synchronized and reconciled with Central. Global cross-Branch coupon/usage caps must not be represented as globally exact while disconnected unless a separately approved quota/reservation mechanism guarantees that property.

### TD-30.5 — Supplier Payments

**Approved choice: B — Central-only supplier payment authority.**
- Central owns supplier balances, payment recording, settlement and payment authorization.
- Branches may submit receiving facts, supplier invoice evidence or payment-related requests where the workflow requires them, but cannot independently execute or confirm supplier payments.
- Supplier payment allocation and settlement remain Central transactions.
- Whether one payment may settle multiple supplier invoices remains a detailed allocation rule to confirm against the TD-17 model before that workflow is implemented; this does not change Central ownership.

### TD-30.6 — Rounding

**Approved choice: C — preserve calculation precision and round at the defined invoice boundary.**
- Preserve the approved internal monetary precision of `decimal(18,3)`.
- Do not round intermediate unit-price, discount or tax calculations merely for display.
- Apply one centrally specified and consistently implemented rounding policy at the final invoice amount/display boundary, to two decimal places as already approved.
- The exact midpoint tie-breaker (for example, ToEven versus AwayFromZero) and the precise points at which invoice components are rounded remain an implementation detail to specify and test before financial code is written. This document does not silently choose one.

### TD-30.7 — Promotion Rules

**Approved choice: C — stacking is explicitly configured per promotion.**
- Promotions do not stack by default.
- A promotion may participate in stacking only when its approved configuration explicitly permits it.
- The promotion engine must apply a deterministic, documented order and compatibility rules when multiple promotions qualify.
- Condition/operator and reward types remain allowlisted, versioned application contracts; arbitrary database expressions are prohibited.
- The full operator catalogue and deterministic precedence are implementation specifications, not new business-policy choices.

### TD-30.8 — Data Retention

**Approved choice: C — preserve financial/audit history and archive operational history.**
- Financial records, issued invoices, payment/refund evidence and audit history are retained as durable historical records and are not routinely deleted as operational cleanup.
- Older operational/technical records may be archived or purged according to approved retention schedules, privacy obligations and legal requirements.
- Archival must preserve traceability, referential meaning, access controls and the ability to investigate historical transactions.
- Exact retention periods, redaction rules, attachment handling, legal holds and archival storage controls must be specified before production rollout. “Retain permanently” means no routine application-level deletion; it does not override applicable law or a valid privacy/legal requirement.

## 3. Implementation and schema boundary

These approvals settle the selected policy for the eight decisions. They do not by themselves complete every detailed implementation specification mentioned above.

Before the affected modules are implemented, the team must still:
1. Specify and test the Offline credential-verifier threat model and key lifecycle.
2. Define the exact SQL conditional-update/rowversion algorithms and transaction boundaries for stock and refunds.
3. Define Offline promotion/coupon quota behavior without claiming unavailable global guarantees.
4. Reconcile supplier payment allocation with TD-17.
5. Select and test the invoice midpoint tie-breaker and component-rounding boundaries.
6. Finalize the promotion operator catalogue and deterministic precedence.
7. Define retention periods, privacy/redaction rules and archival controls.

**No code, database tables, EF Core entities, DDL or migrations are authorized by TD-30.** Existing project sequencing and the requirement for final schema/ERD approval remain in force.
