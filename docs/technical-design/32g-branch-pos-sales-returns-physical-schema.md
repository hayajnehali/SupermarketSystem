# TD-32G — Branch POS, Sales, Invoices, Returns and Shifts Physical Schema Decisions

**Version:** 0.1  
**Status:** Delegated design approval — approved for physical mapping.  
**Date:** 2026-11-28  
**Depends on:** TD-20, TD-21, TD-26, TD-30, TD-31 and TD-32.

## 1. POS ownership

- Each Branch is authoritative for its own POS invoices, invoice lines/discounts, batch issues, cashier shifts, cash drawer, returns, inspections, dispositions and exchange links.
- Central receives immutable synchronized history/projections for reporting, audit and reconciliation. Central does not edit Branch invoice/stock facts.
- Completed financial and stock records are never deleted or rewritten; corrections use cancellation, refund, reversal or compensating movements.

## 2. Invoice numbering and completion

1. Invoice format is `{BranchCode}-{SequenceYear}-{SequenceNumber:D6}`, e.g. `BR01-2026-000001`.
2. `SequenceYear` is derived from the Branch-local business date using the Central-approved Branch time zone.
3. `InvoiceSequences` is a Branch-local counter scoped by Branch + SequenceYear. It is not a PK.
4. Allocate the next number atomically in the same Branch transaction that commits the completed sale. Failed payment attempts before sale completion do not consume an invoice number.
5. A committed invoice number is never reused. Gaps are allowed, including when an issued invoice is later cancelled.
6. Enforce unique (BranchId, SequenceYear, SequenceNumber) and unique formatted InvoiceNumber.
7. Final invoice payable amount is rounded once at the approved boundary to two decimals using TD-31 AwayFromZero. Intermediate unit, quantity, discount and allocation calculations retain decimal(18,3) precision; no component rounding is performed unless a future statutory requirement is approved.

## 3. Atomic POS sale transaction

The following must commit in one Branch database transaction:
- invoice header and line snapshots;
- promotion/coupon discount snapshots;
- exact FEFO/FIFO inventory batch issues and InventoryMovement header/lines;
- confirmed tender/payment transaction and SalePaymentAllocations;
- cash drawer transaction for the cash portion only;
- durable Outbox message and ProcessedOperations/idempotency result.

A sale is completed only after confirmed tender allocations cover the final payable amount. A failed/declined terminal attempt does not complete the invoice or issue stock.

### Independent bank terminal
- The POS bank terminal remains independent and is not integrated through an SDK/API/webhook.
- Cashier manually records the terminal outcome and optional bank reference/receipt evidence.
- Store who recorded the outcome, time, POS device, shift, amount, method and reference; do not claim automated provider verification.
- A terminal result is accepted only through the authorized cashier workflow. Duplicate submission is idempotent.

## 4. Cashier shifts and cash drawer

- Enforce one active shift per Cashier per Branch, regardless of how many POS devices exist. Use a filtered unique index over BranchId + CashierEmployeeId for active/open shifts.
- A shift has one assigned POS device at opening; device/cashier/branch context is snapshotted on every drawer transaction.
- Cash drawer transactions are append-only financial events. Opening float, cash sale, cash refund, cash-in/out and approved deposit/adjustment are separate typed events.
- Sale/refund and corresponding drawer transaction are committed atomically.
- Shift close stores expected cash, counted cash, difference and required reason for a discrepancy. Reconciliation does not rewrite prior drawer events.
- Do not allow editing/deleting a posted drawer transaction; use a compensating cash-in/out entry with actor, reason and audit.

## 5. Invoice snapshots and print/reprint

- Invoice header/lines retain product name/SKU, unit name/code, quantity, base unit price, discounts, promotion/coupon IDs and versions, final amounts, currency and rounding result as immutable snapshots.
- Batch issue records retain exact batch IDs and actual batch costs for COGS. Composite product sales retain each component issue and cost.
- Reprint uses the retained invoice snapshot/document version. It never creates another invoice, sequence number, payment or stock movement.
- `InvoicePrintRecords` is an operational audit record for print/reprint actor, device, time, document version and result; retention follows the approved retention schedule. No fixed retention duration is invented here.

## 6. Returns, inspection and exchanges

1. Every ordinary return references the original invoice and original invoice line. Exceptional no-invoice return requires Central decision before completion.
2. Returned quantity retains original invoice-line and batch-issue lineage, even if the original batch is depleted, transferred or closed.
3. A return line may map to multiple original batch issue rows. `SalesReturnBatchAllocations` records the exact original batch quantity/cost for each portion.
4. Inspection is recorded per line and quantity. One returned line may be split across multiple outcomes: sellable, quarantine/review, or damaged/expired.
5. Only inspected quantities approved as sellable may be posted back to available stock. Quarantined/damaged/expired quantities remain unavailable and follow the Central disposal process where required.
6. Return-to-stock or quarantine disposition and the corresponding Branch inventory movement commit atomically.
7. Refund is cash-only regardless of original tender. If any original successful tender was electronic, the cash refund requires prior Central approval and a recorded reason. Refund allocations remain linked proportionally to the original successful tenders under TD-31; no provider refund is executed.
8. Refund amount cannot exceed original paid amount or the remaining refundable amount of each original tender. The refund control row and cash drawer posting are concurrency-controlled in one transaction.
9. An exchange is a linked completed return plus a separate replacement sale; the two invoices/payment/stock effects are never merged.

## 7. Paid invoice cancellation

- A paid invoice is never deleted or directly changed to erase its financial effect.
- Cancellation requires the approved cancellation workflow and Central decision where the policy requires it.
- The Branch applies the authorized cancellation command idempotently and creates compensating refund/stock movement records as applicable.
- The original invoice, payments, batch issue, cancellation request/decision and compensating records remain traceable.
- A duplicate cancellation command returns the original result and cannot reverse stock or cash twice.

## 8. Physical constraints and indexes

- Unique Branch/year/sequence and formatted invoice number.
- Filtered unique active shift per Branch/Cashier.
- Append-only CashDrawerTransactions and posted InventoryMovements.
- Unique source OperationId/purpose keys for sale completion, return posting, refund posting and cancellation compensation.
- Positive invoice/return quantities, non-negative amounts and discount caps; stable status/type codes with named CHECK constraints.
- RowVersion on mutable shift, invoice workflow, return workflow and refund-control rows; immutable ledger/event rows do not require update-oriented rowversion.
- Local FKs only. Central approval IDs, online order IDs and external bank references are GUID/string correlations, not cross-database FKs.

## 9. Required tests

- Two devices attempting to open a shift for the same cashier.
- Concurrent invoice number allocation around year boundary and Branch time zone changes.
- Failed terminal attempt, retry and duplicate cashier confirmation.
- Atomic rollback across invoice, batch issue, payment allocation, drawer and Outbox.
- Return line split across original batch issues where one batch is depleted.
- Mixed sellable/quarantine/damaged inspection quantities.
- Concurrent refunds against the same invoice and original tender cap.
- Paid cancellation replay and compensation idempotency.
- Reprint does not create new financial or inventory effects.

## 10. Approval record

The owner delegated detailed design decisions on 2026-11-28. This Branch POS/Sales/Returns physical design is approved for mapping. SQL sequence allocation and refund/stock concurrency algorithms must be verified with SQL Server 2025 integration tests.
