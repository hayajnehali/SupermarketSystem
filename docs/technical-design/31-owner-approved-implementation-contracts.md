# TD-31 — Owner-Approved Implementation Contracts and Remaining Technical Specifications

**Status:** Approved by owner (2026-11-28) — implementation contracts recorded  
**Depends on:** TD-13 through TD-30 and the approved business requirements.  
**Purpose:** Record the implementation-level choices accepted by the owner in the consolidated design review. This document refines implementation behavior; it does not authorize production DDL, EF Core entities, migrations, or feature code.

## 1. Approval scope

The owner approved the recommendations for:
- Logical schema package TD-13–TD-25, subject to cross-document reconciliation and the explicit exceptions below.
- SQL Server / EF Core standards TD-26.
- Clean Architecture and Pragmatic DDD TD-27.
- Solution/project structure TD-28.
- The implementation contracts in this document, while preserving TD-30 decisions.

Approval of the logical schema package does not mean the Central and Branch physical schemas have already been generated or approved. Those remain separate deliverables under TD-32.

## 2. Payment-provider idempotency and callback processing

**Approved contract:**
1. Every payment initiation has a stable application OperationId and provider-scoped idempotency key.
2. Every provider callback is authenticated by validating the provider signature/verification mechanism before business processing.
3. Provider event identifiers are unique within the provider/account scope and are persisted in the durable Inbox/provider-event store.
4. Duplicate callbacks return the previously recorded outcome and must not create another payment, order transition, or financial posting.
5. Callback arrival order is not trusted. The payment state machine accepts only valid forward transitions; an older or contradictory event is retained for audit/reconciliation and cannot silently regress a terminal state.
6. An ambiguous timeout is resolved by querying/reconciling with the provider using the original operation/reference. A new payment attempt is not created until the prior attempt's outcome is resolved or explicitly classified as a separate attempt.
7. Provider event receipt, event processing outcome, and business payment state are separately traceable.

Provider-specific signature algorithms, API retry limits and exact event mappings belong in the provider adapter specification; they must not be generalized beyond the selected provider's documented contract.

## 3. Split-tender refund allocation

**Approved contract: proportional allocation to original successful payment transactions.**
1. A refund is linked to the original invoice and the original successful payment transaction(s).
2. The requested refund amount is allocated proportionally according to the amounts contributed by the original successful tenders.
3. Each allocation is bounded by the remaining refundable amount of its original payment transaction.
4. Currency precision is preserved during allocation. Any final minor-unit remainder caused by rounding is assigned deterministically to the last eligible allocation in a stable order, without exceeding its refundable cap.
5. The transaction validates cumulative successful refunds against both the original invoice paid amount and each original payment transaction.
6. Customer refund payout remains cash-only under the previously approved policy. The allocation describes the source-payment relationship; it does not execute a provider refund.

## 4. Online-order cash refund handover

**Approved contract: Central approval, Branch cash payout.**
1. Customer refund request is registered against the online order and original payment.
2. Central validates eligibility and records approval, reason and authorized approver.
3. Central assigns the payout to the responsible Branch and issues a durable, idempotent command.
4. Branch verifies the customer/order using the approved verification workflow and confirms that the command is valid and not already completed.
5. Branch pays cash from the authorized drawer and records payout, cashier, shift, drawer transaction, timestamp and receipt/evidence reference.
6. Branch acknowledges the command with the durable result; Central reconciles the acknowledgement and closes the refund workflow.
7. A repeated command or acknowledgement cannot cause a second payout.

Required customer verification factors and evidence retention must follow TD-06/TD-07; no new customer authentication method is introduced here.

## 5. Offline promotions, coupons and loyalty

### 5.1 Promotions and coupons
- Branch uses only complete, approved, versioned Central snapshots within their validity window, as established by TD-30.
- Expired snapshots are not applied and cannot be extended locally.
- A coupon or promotion with a globally limited cross-Branch usage cap is not eligible for Offline redemption unless Central has issued a finite Branch-specific quota/reservation that guarantees the cap.
- Where no such quota exists, Branch rejects that limited redemption while Offline. No global exactness is claimed from eventually synchronized usage.

### 5.2 Loyalty
- Offline sales may create pending earned-point events based on the last approved local rules snapshot, subject to reconciliation by Central.
- Loyalty redemption is Online-only; Branch must not spend a potentially stale balance while disconnected.
- Central is authoritative for the ledger and available balance. Duplicate earning events are prevented by stable operation identifiers.

## 6. Supplier payment allocation

**Approved contract: one Central payment may settle multiple supplier invoices.**
- Central remains the sole authority for supplier payment initiation/recording/settlement.
- The logical model must represent a payment header separately from its invoice allocations.
- One payment has one or more allocation rows; each allocation references one supplier invoice.
- The sum of allocations must not exceed the payment amount; each invoice's allocated total must not exceed its outstanding payable amount.
- Allocation posting and payment status update occur in one Central database transaction.
- Branches may submit receiving facts and supporting supplier-invoice evidence only; they cannot confirm supplier payment.

**Required TD-17 reconciliation:** replace the one-payment-to-one-invoice assumption with SupplierPayments plus SupplierPaymentAllocations (or equivalent names consistent with TD-17 naming). Do not create physical tables until the Central schema package is approved.

## 7. JOD rounding

**Approved contract: AwayFromZero at the final invoice rounding boundary.**
- Internal monetary values remain decimal(18,3).
- Intermediate unit-price, discount and calculation steps retain full approved precision and are not rounded for display.
- The final invoice payable/display amount is rounded to two decimal places using .NET MidpointRounding.AwayFromZero semantics.
- The same canonical rounding function and test vectors must be used by Central and Branch. SQL calculations must not independently apply a conflicting rule.
- Component-level rounding is not performed unless a specific statutory/provider contract later requires it and is approved as a separate rule.
- Tests cover positive and negative midpoint values, values immediately below/above midpoint, discounts, returns and cumulative allocations.

## 8. Promotion rule catalogue and precedence

- Promotion definitions reference allowlisted condition/operator and reward types implemented by versioned application contracts.
- No arbitrary SQL, C#, script, expression tree or executable expression is stored in or evaluated from the database.
- Promotions do not stack by default. Explicitly stackable promotions are evaluated using deterministic precedence: configured priority descending, then stable PromotionId ascending as the tie-breaker.
- The engine validates compatibility and prevents the same eligible quantity/value from being consumed twice unless the approved promotion type explicitly permits it.
- The selected promotion version and applied calculation details are snapshotted on the sale so later rule edits do not rewrite historical invoices.
- The concrete operator/reward catalogue must match types already modeled in TD-19 and be versioned before promotion implementation; adding a new reward type requires review and tests.

## 9. Receiving and stocktake recovery

- Every Central command has a unique CommandId, aggregate/business reference, schema version, issued timestamp and idempotency scope.
- Branch durably records receipt before acknowledging delivery.
- Branch applies the command and all local stock effects in one Branch transaction, together with the processed-operation record and resulting Outbox event.
- A duplicate command returns the stored result without repeating stock effects.
- Stocktake locks are durable database state, scoped to the affected stocktake/session and stock scope; every stock-changing operation checks them inside its transaction.
- Restart/recovery resumes from durable command/session state. Locks are released only by the approved completion/cancellation command, not by process restart or a local timeout.
- Central treats the Branch acknowledgement as the source of completion for the physical Branch action.

## 10. Concurrency contracts

### 10.1 Stock reservation and sale
- Use rowversion to detect stale writes on mutable stock/balance rows.
- The database operation also conditionally verifies sufficient available quantity and applies the decrement/reservation atomically.
- FIFO/FEFO batch selection and exact batch allocations are persisted in the same Branch transaction as the stock movement and sale/reservation.
- A conflict reloads and re-evaluates availability; no retry may bypass the stock condition.
- Stock must never become negative.

### 10.2 Refund cap
- Refund requests are idempotent by OperationId.
- A transaction serializes competing changes through a concurrency-controlled refund-control row associated with the original invoice/payment, then rechecks the remaining refundable amount before inserting the refund and cash-drawer transaction.
- A stale rowversion causes the request to reload/re-evaluate; successful cumulative refunds cannot exceed the paid amount or any original tender's refundable cap.

### 10.3 Invoice sequence
- Allocate invoice sequence numbers with an atomic database counter operation scoped by Branch and sequence year.
- Enforce uniqueness for (BranchId, SequenceYear, SequenceNumber) and the formatted InvoiceNumber.
- Gaps are allowed; numbers are never reused after a committed allocation.

### 10.4 Shift and drawer posting
- Cash drawer postings are append-only financial events and are committed in the same transaction as the sale/refund/approved drawer operation they represent.
- Enforce one active shift per cashier per Branch, regardless of POS device, using a database uniqueness mechanism compatible with the final physical model.
- Every posting carries Branch, POS device, cashier and shift identifiers.

### 10.5 Outbox
- Business state change and Outbox message are committed in the same local database transaction.
- Outbox claiming uses an atomic lease/claim operation; never use a read-then-update claim race.
- Inbox/ProcessedOperations uniqueness prevents duplicate business effects.

Exact SQL statements, lock hints, indexes and test schedules are finalized in the physical module specifications and must be proven with SQL Server integration/concurrency tests.

## 11. POS operational contracts

- Invoice-number gaps are acceptable; an allocated/committed number is never reused.
- Returns retain their original invoice-line and batch-issue lineage even if the original batch has been depleted or transferred.
- Returned quantities are inspected and dispositioned by authorized staff; only accepted saleable quantities may return to available stock.
- Mixed inspection outcomes are represented per returned line/quantity, not as one blanket decision for the entire return.
- A paid cancellation uses an explicit compensating/refund workflow; it never deletes the original sale/payment.
- Offline decisions and their evidence are durable and synchronized idempotently; sensitive decisions that require Central approval remain blocked until authorization is available.
- Reprint uses the retained invoice snapshot/document version and does not create a new invoice.
- One cashier may have only one active shift per Branch at a time, even when multiple POS devices exist.

## 12. Reporting calculation contracts

- Net sales are calculated from completed sales less completed returns/cancellations according to the approved reporting definition; pending/failed transactions are excluded.
- Cost of goods sold uses the actual cost of the specific inventory batches issued to the sale, including the approved FIFO/FEFO allocation.
- Gross profit is net sales less the corresponding actual cost of goods sold; current supplier price or current product cost must never be substituted for historical batch cost.
- Branch comparisons use synchronized, traceable Branch facts and identify data freshness; Central must not present unsynchronized data as complete.
- Detailed metric definitions, treatment of discounts/rounding and date boundaries are versioned in the reporting specification before dashboards are implemented.

## 13. Retention and privacy implementation gate

TD-30's retention policy remains authoritative. Before production rollout, a retention schedule must identify record category, legal/business basis, retention period, archive tier, redaction/anonymization rules, attachment handling, legal hold behavior, authorized access and deletion/audit evidence. No guessed retention duration is approved by this document.

## 14. Explicitly deferred

- Offline credential-verifier cryptographic construction, key custody/rotation and revocation implementation: must be security-reviewed and tested before Offline login code.
- Provider-specific callback/signature/retry mapping: complete in each provider adapter specification.
- Exact retention periods and jurisdiction-specific privacy/legal requirements: must be validated before production.
- Payroll detailed schema and tax/e-invoicing engine: remain outside the first-release scope; placeholders only.
- Physical Central/Branch column/index/constraint/migration package: TD-32 deliverable.

## 15. Implementation boundary

TD-31 records owner-approved implementation contracts and closes the corresponding policy-choice gates. It does not create production code, tables, EF Core entities, DDL or migrations. The final Central and Branch physical schemas must be separately reviewed and approved before database implementation begins.
