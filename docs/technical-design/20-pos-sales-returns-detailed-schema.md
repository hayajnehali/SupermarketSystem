# TD-20 — Detailed Logical Schema: POS, Sales, Invoices, Returns and Exchanges

**Version:** 0.1  
**Status:** Proposed for owner review  
**Date:** 2026-11-07  
**Scope:** Cashier shifts and drawers, suspended carts, POS invoices and snapshots, invoice numbering/printing, cancellation approvals, returns/inspection/disposition and exchanges.  
**Depends on:** Approved BRS Units 06–07, POS-01–POS-20, PMT-01–PMT-16, RET-02–RET-07, P11–P20, GAP-01–GAP-08, TD-03, TD-04, TD-13 and TD-14.

> This document specifies Branch-owned POS/sales records and their Central synchronized history. Payment execution remains in Payments & Refunds; physical stock remains in Inventory & Stock. It is a design proposal, not executable DDL, EF Core code or a migration.

## 1. Shared conventions and invariants

1. Every PK is application-generated sequential `Guid` / SQL Server `uniqueidentifier`; no integer identity PK.
2. Quantities use `decimal(18,3)`; currency amounts use `decimal(18,3)` for internal JOD calculations. Final JOD invoice amounts are rounded to two decimals under the approved rule; the technical half-rounding tie-breaker remains to be fixed before implementation.
3. All event timestamps use `datetime2(3)` UTC. `SequenceYear` is computed using the Branch's configured business time zone, not UTC year.
4. The Branch database that executed the sale is authoritative for the invoice, shift, drawer, return and local cash operation. Central receives immutable synchronized history/projections.
5. A completed invoice is an immutable financial/operational snapshot. Later changes to product, unit, price, promotion, customer or supplier master data do not rewrite it.
6. A sale cannot complete without an open cashier shift, sufficient sellable stock and fully confirmed payment allocations equal to the full invoice total.
7. Split tender is supported, but no unpaid/credit balance is created.
8. Payment state, invoice state, return inspection/disposition and refund state are separate. A return is not a refund; a refund is not a stock movement.
9. Reprinting reuses the same invoice and number. It creates only a print audit record.
10. Return stock is pending inspection and cannot become sellable until the approved disposition is recorded. Damaged/expired stock remains isolated pending Central disposal approval.
11. A replacement sale in an exchange is a separate invoice linked to the return; neither transaction overwrites the other.

## 2. Database placement

| Table | Central DB | Each Branch DB |
|---|---|---|
| CashierShifts, CashDrawerTransactions, CashDrawerReconciliations | Immutable synchronized history | Authoritative |
| SuspendedSales, SuspendedSaleLines | No | Authoritative local draft |
| SalesInvoices, SalesInvoiceLines, SalesInvoiceDiscounts | Immutable synchronized history | Authoritative |
| SalesInvoiceBatchIssues, CompositeSaleComponentIssues | Immutable synchronized history | Authoritative |
| InvoiceSequences, InvoicePrintRecords | No | Authoritative |
| SalePaymentAllocations | Financial history projection | Authoritative POS allocation record |
| InvoiceCancellationRequests | Central review copy | Branch request origin |
| InvoiceCancellationDecisions | Authoritative | No |
| SalesReturns, SalesReturnLines, SalesReturnBatchAllocations, Inspections, Dispositions | Synchronized history/projection | Authoritative return execution |
| SalesReturnExceptionDecisions | Authoritative | No |
| ExchangeLinks | Synchronized history | Authoritative link |

Branch-originated cancellation/return requests retain the same GUID when synchronized to Central. No cross-database FK is created.

## 3. Cashier shifts and cash drawer

### 3.1 `sales.CashierShifts` — Branch authoritative

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CashierShiftId | uniqueidentifier | No | PK; sequential Guid |
| CashierEmployeeId | uniqueidentifier | No | Employee GUID; Branch authorization snapshot reference |
| POSDeviceId | uniqueidentifier | No | Registered POS device GUID |
| ShiftStatus | tinyint | No | Open / Closing / Closed |
| OpenedAtUtc | datetime2(3) | No | UTC |
| OpeningCashAmount | decimal(18,3) | No | Counted opening float |
| ClosedAtUtc | datetime2(3) | Yes | UTC |
| ClosedByEmployeeId | uniqueidentifier | Yes | Employee GUID |
| CreatedOperationId | uniqueidentifier | No | Idempotent shift opening |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- At most one active shift per cashier/device combination according to the approved operational context; exact uniqueness scope must be enforced transactionally.
- Index CashierEmployeeId + ShiftStatus + OpenedAtUtc and POSDeviceId + ShiftStatus.
- A POS sale requires a valid open shift associated with the active cashier and device.
- Branch database identity supplies BranchId; no Central Branch FK.

### 3.2 `sales.CashDrawerTransactions` — Branch authoritative

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CashDrawerTransactionId | uniqueidentifier | No | PK; sequential Guid |
| CashierShiftId | uniqueidentifier | No | FK → CashierShifts |
| TransactionType | tinyint | No | CashSale / CashRefund / CashIn / CashOut / ApprovedAdjustment |
| Amount | decimal(18,3) | No | Positive amount; direction determined by type |
| SourceTransactionType | nvarchar(80) | Yes | Invoice / Refund / Other |
| SourceTransactionId | uniqueidentifier | Yes | Source GUID |
| RecordedByEmployeeId | uniqueidentifier | No | Actor |
| RecordedAtUtc | datetime2(3) | No | UTC |
| Reason | nvarchar(500) | Yes | Required for manual cash-in/out/adjustment |
| OperationId | uniqueidentifier | No | Idempotency key |

**Constraints**
- FK to CashierShifts; no cascade delete.
- Cashier cannot create an unauthorized adjustment or edit/delete a posted drawer transaction.
- Cash sales/refunds link to their originating invoice/refund allocation.

### 3.3 `sales.CashDrawerReconciliations` — Branch authoritative

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CashDrawerReconciliationId | uniqueidentifier | No | PK; sequential Guid |
| CashierShiftId | uniqueidentifier | No | Unique FK → CashierShifts |
| ExpectedCashAmount | decimal(18,3) | No | Calculated from opening float and posted drawer transactions |
| CountedCashAmount | decimal(18,3) | No | Actual counted amount |
| DifferenceAmount | decimal(18,3) | No | Counted minus expected |
| DifferenceReason | nvarchar(1000) | Yes | Reason when provided |
| ReconciledByEmployeeId | uniqueidentifier | No | Closing cashier/authorized actor |
| ReconciledAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- One reconciliation per shift; immutable after shift close.
- ExpectedCashAmount is system-calculated and cannot be edited by Cashier.
- Difference is retained for Central review; cashier cannot delete or alter it.

## 4. Suspended POS carts

### 4.1 `sales.SuspendedSales` and `SuspendedSaleLines` — Branch

**`SuspendedSales`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SuspendedSaleId | uniqueidentifier | No | PK; sequential Guid |
| CashierShiftId | uniqueidentifier | No | FK → CashierShifts |
| CashierEmployeeId | uniqueidentifier | No | Employee GUID |
| POSDeviceId | uniqueidentifier | No | Device GUID |
| SuspendedStatus | tinyint | No | Suspended / Resumed / Cancelled |
| CreatedAtUtc | datetime2(3) | No | UTC |
| SuspendedAtUtc | datetime2(3) | No | UTC |
| ResumedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

**`SuspendedSaleLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SuspendedSaleLineId | uniqueidentifier | No | PK; sequential Guid |
| SuspendedSaleId | uniqueidentifier | No | FK → SuspendedSales |
| ProductId | uniqueidentifier | No | Product GUID |
| ProductUnitId | uniqueidentifier | No | Product unit GUID |
| Quantity | decimal(18,3) | No | Positive quantity |
| LastDisplayedUnitPrice | decimal(18,3) | Yes | Informational only; never authoritative on resume |
| AddedAtUtc | datetime2(3) | No | UTC |

**Constraints and indexes**
- SuspendedSale → Lines is 1:N.
- A suspended cart is not an invoice and does not deduct stock or allocate payment.
- On resume, all prices/promotions are recalculated using current approved data; changed values are shown and cashier confirmation is required before completion.
- The old displayed price cannot be used to bypass an expired price/promotion.

## 5. POS invoices and line snapshots

### 5.1 `sales.InvoiceSequences` — Branch

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InvoiceSequenceId | uniqueidentifier | No | PK; sequential Guid |
| SequenceYear | int | No | Branch-local business year |
| LastIssuedNumber | bigint | No | Monotonic counter |
| UpdatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique SequenceYear within the Branch DB.
- Sequence allocation is atomic and transaction-safe; concurrent cashiers cannot receive the same number.
- The counter is not a primary key. No integer identity is used.
- Invoice format is `BR01-YYYY-000001`; BranchCode + SequenceYear + SequenceNumber is unique.
- SequenceYear uses the configured Branch time zone. Whether unused numbers can be skipped after a failed completion is an implementation detail; uniqueness and non-reuse are mandatory.

### 5.2 `sales.SalesInvoices` — Branch authoritative; Central immutable history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SalesInvoiceId | uniqueidentifier | No | PK; sequential Guid |
| InvoiceNumber | nvarchar(40) | No | Human-readable branch/year/sequence number |
| SequenceYear | int | No | Branch-local year |
| SequenceNumber | bigint | No | Branch/year sequence |
| CashierShiftId | uniqueidentifier | No | FK → CashierShifts |
| CashierEmployeeId | uniqueidentifier | No | Employee GUID snapshot/reference |
| POSDeviceId | uniqueidentifier | No | Device GUID |
| CustomerId | uniqueidentifier | Yes | Customer GUID if customer identified |
| InvoiceStatus | tinyint | No | Draft / Completed / Cancelled |
| CurrencyCode | char(3) | No | JOD for current project |
| SubtotalAmount | decimal(18,3) | No | Pre-discount subtotal |
| DiscountAmount | decimal(18,3) | No | Total applied discounts |
| FinalAmount | decimal(18,3) | No | Final payable amount at approved precision |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CompletedAtUtc | datetime2(3) | Yes | UTC |
| CancelledAtUtc | datetime2(3) | Yes | UTC |
| CancelledByEmployeeId | uniqueidentifier | Yes | Actor |
| CancellationReason | nvarchar(1000) | Yes | Required for cancellation |
| OperationId | uniqueidentifier | No | Idempotent sale completion |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique InvoiceNumber and unique SequenceYear + SequenceNumber.
- FK CashierShift; restrict delete.
- Invoice completion requires an open shift, available stock and confirmed full payment.
- Completed invoice values and references are immutable; correction uses an approved cancellation/return process.
- Historical invoice line snapshots remain valid even if the current product is deactivated or renamed.
- Tax remains placeholder only; no tax calculation fields are introduced beyond approved placeholders.

### 5.3 `sales.SalesInvoiceLines` — Branch authoritative; Central immutable history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SalesInvoiceLineId | uniqueidentifier | No | PK; sequential Guid |
| SalesInvoiceId | uniqueidentifier | No | FK → SalesInvoices |
| ProductId | uniqueidentifier | No | Product GUID |
| ProductUnitId | uniqueidentifier | No | Product unit GUID |
| SKU | nvarchar(60) | No | Historical SKU snapshot |
| ProductName | nvarchar(200) | No | Historical name snapshot |
| Quantity | decimal(18,3) | No | Sold quantity |
| BaseQuantity | decimal(18,3) | No | Converted inventory quantity |
| ConversionFactorSnapshot | decimal(18,6) | No | Conversion used at sale |
| UnitPrice | decimal(18,3) | No | Approved unit price snapshot |
| GrossLineAmount | decimal(18,3) | No | Quantity × unit price before discount |
| DiscountAmount | decimal(18,3) | No | Line-level discount total |
| NetLineAmount | decimal(18,3) | No | Final line amount |
| WeightSource | tinyint | Yes | Scale / Manual; used for weighed products |
| ManualWeightEnteredByEmployeeId | uniqueidentifier | Yes | Required when WeightSource = Manual |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Invoice → Lines is 1:N.
- Quantity and BaseQuantity are positive; configured ProductUnit precision is enforced.
- If WeightSource = Manual, ManualWeightEnteredByEmployeeId is required.
- Historical product name, SKU, unit, conversion, price and discount values are snapshots; never recompute them from current Catalog/Pricing.

### 5.4 `sales.SalesInvoiceDiscounts` — Branch authoritative; Central immutable history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SalesInvoiceDiscountId | uniqueidentifier | No | PK; sequential Guid |
| SalesInvoiceId | uniqueidentifier | No | FK → SalesInvoices |
| SalesInvoiceLineId | uniqueidentifier | Yes | Optional FK → SalesInvoiceLines; null for basket-level discount |
| DiscountSourceType | tinyint | No | ProductPromotion / BasketPromotion / Coupon |
| SourceDefinitionId | uniqueidentifier | Yes | Promotion/Coupon GUID |
| SourceVersion | bigint | Yes | Applied publication/version reference |
| DiscountDescription | nvarchar(300) | No | Historical display description |
| DiscountAmount | decimal(18,3) | No | Actual applied amount |
| AppliedAtUtc | datetime2(3) | No | UTC |

**Constraints**
- FK to invoice and optional invoice line.
- SourceDefinitionId is a GUID reference to central promotion/coupon definition; Branch has local approved projection when applied Offline.
- Sum of discount components must reconcile to invoice/line discount totals.
- Duplicate application of the same discount to the same transaction scope is prohibited.

### 5.5 `sales.SalesInvoiceBatchIssues` and `CompositeSaleComponentIssues` — Branch

**`SalesInvoiceBatchIssues`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SalesInvoiceBatchIssueId | uniqueidentifier | No | PK; sequential Guid |
| SalesInvoiceLineId | uniqueidentifier | No | FK → SalesInvoiceLines |
| InventoryBatchId | uniqueidentifier | No | FK → local InventoryBatches |
| QuantityIssued | decimal(18,3) | No | Positive quantity |
| UnitCostSnapshot | decimal(18,3) | No | Actual batch unit cost |
| CostAmount | decimal(18,3) | No | Quantity × actual batch cost |

**`CompositeSaleComponentIssues`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CompositeSaleComponentIssueId | uniqueidentifier | No | PK; sequential Guid |
| SalesInvoiceLineId | uniqueidentifier | No | FK → composite product invoice line |
| ComponentProductId | uniqueidentifier | No | Component Product GUID |
| ComponentProductUnitId | uniqueidentifier | No | Component unit GUID |
| InventoryBatchId | uniqueidentifier | No | FK → local InventoryBatches |
| QuantityConsumed | decimal(18,3) | No | Actual component quantity |
| UnitCostSnapshot | decimal(18,3) | No | Actual component batch cost |
| CostAmount | decimal(18,3) | No | Quantity × component batch cost |

**Constraints and indexes**
- Each normal invoice line's batch issue quantities sum to its inventory base quantity.
- Composite component issue rows record the actual component quantities and costs consumed; the composite invoice line retains the single approved total selling price.
- FIFO applies to non-expiry-tracked products; FEFO applies to expiry-tracked products. Expired/quarantined/reserved stock cannot be issued.
- COGS is derived from actual batch issue costs and never from average product cost.

### 5.6 `sales.SalePaymentAllocations` — Branch authoritative; Central financial history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SalePaymentAllocationId | uniqueidentifier | No | PK; sequential Guid |
| SalesInvoiceId | uniqueidentifier | No | FK → SalesInvoices |
| PaymentTransactionId | uniqueidentifier | No | Local Payments transaction GUID |
| PaymentMethodId | uniqueidentifier | No | Approved payment-method GUID/projection |
| AllocatedAmount | decimal(18,3) | No | Positive allocation |
| AllocationStatus | tinyint | No | Pending / Confirmed / Failed / Reversed |
| AllocatedAtUtc | datetime2(3) | No | UTC |
| OperationId | uniqueidentifier | No | Idempotency key |

**Constraints**
- Invoice → payment allocations is 1:N.
- Confirmed allocations must sum exactly to the full final invoice amount before invoice completion.
- Failed/pending/unconfirmed electronic allocation does not complete the sale.
- Split cash/electronic tender is supported; no remaining balance/credit record is created.
- Payment transaction details and provider confirmation remain owned by Payments & Refunds.

### 5.7 `sales.InvoicePrintRecords` — Branch

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InvoicePrintRecordId | uniqueidentifier | No | PK; sequential Guid |
| SalesInvoiceId | uniqueidentifier | No | FK → SalesInvoices |
| PrintedByEmployeeId | uniqueidentifier | No | Actor |
| PrintedAtUtc | datetime2(3) | No | UTC |
| PrintType | tinyint | No | Original / Reprint |
| CopySequence | int | No | Monotonic per invoice |
| DeviceId | uniqueidentifier | Yes | Printer/POS device GUID |

**Constraints**
- Reprint records reference the same SalesInvoiceId and InvoiceNumber; they never allocate a new sequence or inventory movement.

## 6. Invoice cancellation

### 6.1 `sales.InvoiceCancellationRequests` — Branch request origin; Central review copy

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InvoiceCancellationRequestId | uniqueidentifier | No | PK; generated at Branch and preserved centrally |
| SalesInvoiceId | uniqueidentifier | No | Invoice GUID |
| RequestedByEmployeeId | uniqueidentifier | No | Branch actor |
| RequestedAtUtc | datetime2(3) | No | UTC |
| Reason | nvarchar(1000) | No | Required reason |
| RequestStatus | tinyint | No | Submitted / Approved / Rejected |
| RowVersion | rowversion | No | Concurrency token |

### 6.2 `sales.InvoiceCancellationDecisions` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InvoiceCancellationDecisionId | uniqueidentifier | No | PK; sequential Guid |
| InvoiceCancellationRequestId | uniqueidentifier | No | FK → Central request copy |
| Decision | tinyint | No | Approve / Reject |
| Reason | nvarchar(1000) | No | Central decision reason |
| DecidedByUserId | uniqueidentifier | No | Central actor |
| DecidedAtUtc | datetime2(3) | No | UTC |

**Rules**
- Unpaid invoice cancellation follows branch permissions and approved workflow.
- Paid invoice cancellation requires Central approval and recorded reason.
- Central decision is sent to Branch as an authorized command; Branch applies the status and any required compensating inventory/payment effects atomically within its local boundaries.

## 7. Returns, inspection and exchanges

### 7.1 `sales.SalesReturns` and `SalesReturnLines` — Branch authoritative; Central synchronized history

**`SalesReturns`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SalesReturnId | uniqueidentifier | No | PK; sequential Guid |
| ReturnReference | nvarchar(50) | No | Unique Branch return reference |
| OriginalSalesInvoiceId | uniqueidentifier | Yes | Original invoice GUID; null only for Central-approved exception |
| CustomerId | uniqueidentifier | Yes | Customer GUID where identified |
| ReturnStatus | tinyint | No | Requested / PendingInspection / Inspected / DispositionPending / Completed / Rejected |
| RecordedByEmployeeId | uniqueidentifier | No | Authorized Branch employee |
| RecordedAtUtc | datetime2(3) | No | UTC |
| Reason | nvarchar(1000) | No | Return reason |
| RowVersion | rowversion | No | Concurrency token |

**`SalesReturnLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SalesReturnLineId | uniqueidentifier | No | PK; sequential Guid |
| SalesReturnId | uniqueidentifier | No | FK → SalesReturns |
| OriginalSalesInvoiceLineId | uniqueidentifier | Yes | Original line GUID; required for normal return |
| ProductId | uniqueidentifier | No | Product GUID |
| ProductUnitId | uniqueidentifier | No | Unit GUID |
| ReturnedQuantity | decimal(18,3) | No | Positive returned quantity/weight |
| OriginalUnitPriceSnapshot | decimal(18,3) | No | Original sale price |
| OriginalDiscountSnapshot | decimal(18,3) | No | Original applied discount |
| EligibleRefundAmount | decimal(18,3) | No | Amount attributable to returned portion |
| RowVersion | rowversion | No | Concurrency token |

### 7.2 `sales.SalesReturnBatchAllocations` — Branch authoritative; Central synchronized history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SalesReturnBatchAllocationId | uniqueidentifier | No | PK; sequential Guid |
| SalesReturnLineId | uniqueidentifier | No | FK → SalesReturnLines |
| SalesInvoiceBatchIssueId | uniqueidentifier | No | FK → original SalesInvoiceBatchIssues |
| ReturnedQuantity | decimal(18,3) | No | Portion returned from this original batch issue |
| UnitCostSnapshot | decimal(18,3) | No | Original batch cost |
| CostAmount | decimal(18,3) | No | Returned quantity × original cost |

**Constraints**
- A return line may map to multiple original batch issues.
- Sum of batch allocations equals returned quantity after unit conversion.
- Cumulative returned quantity/weight cannot exceed the eligible original sold quantity after prior returns.
- Original price, discount and batch cost are preserved; no current price or average cost is substituted.

### 7.3 `sales.SalesReturnInspections` — Branch execution; Central synchronized history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SalesReturnInspectionId | uniqueidentifier | No | PK; sequential Guid |
| SalesReturnLineId | uniqueidentifier | No | FK → SalesReturnLines |
| InspectionOutcome | tinyint | No | Sellable / QuarantineOrReview / DamagedOrExpired |
| InspectionNotes | nvarchar(1000) | Yes | Findings |
| InspectedByEmployeeId | uniqueidentifier | No | Authorized inspector |
| InspectedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

### 7.4 `sales.SalesReturnDispositions` — Branch execution; Central synchronized history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SalesReturnDispositionId | uniqueidentifier | No | PK; sequential Guid |
| SalesReturnLineId | uniqueidentifier | No | FK → SalesReturnLines |
| DispositionType | tinyint | No | ReturnToSellableStock / Quarantine / AwaitCentralDisposalApproval |
| Quantity | decimal(18,3) | No | Positive disposition quantity |
| InventoryMovementId | uniqueidentifier | Yes | Local inventory movement GUID when posted |
| InventoryQuarantineId | uniqueidentifier | Yes | Local quarantine GUID when quarantined |
| InventoryDisposalRequestId | uniqueidentifier | Yes | Central disposal request GUID when submitted |
| DecidedByEmployeeId | uniqueidentifier | No | Authorized verifier/actor |
| DispositionAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- No returned quantity is sellable before inspection.
- Sellable items return to stock only after inspection confirms eligibility and the corresponding Inventory movement is committed.
- Unresolved items remain isolated/quarantined.
- Damaged/expired items remain isolated until Central approves disposal; Branch records actual destruction separately in Inventory.
- Disposition quantities cannot exceed inspected/returned quantities.

### 7.5 `sales.SalesReturnExceptionDecisions` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SalesReturnExceptionDecisionId | uniqueidentifier | No | PK; sequential Guid |
| SalesReturnId | uniqueidentifier | No | Central return GUID |
| Decision | tinyint | No | Approve / Reject |
| Reason | nvarchar(1000) | No | Central exception decision |
| DecidedByUserId | uniqueidentifier | No | Central actor |
| DecidedAtUtc | datetime2(3) | No | UTC |

A return without an original invoice is an exception and must be referred to Central before processing proceeds.

### 7.6 `sales.ExchangeLinks` — Branch authoritative; Central synchronized history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ExchangeLinkId | uniqueidentifier | No | PK; sequential Guid |
| SalesReturnId | uniqueidentifier | No | FK → SalesReturns |
| ReplacementSalesInvoiceId | uniqueidentifier | No | FK → SalesInvoices |
| LinkedAtUtc | datetime2(3) | No | UTC |
| LinkedByEmployeeId | uniqueidentifier | No | Actor |
| PriceDifferenceAmount | decimal(18,3) | No | Difference snapshot; settlement uses Payments allocation/refund rules |

**Constraints**
- An exchange is a linked return plus a separate replacement invoice.
- Replacement sale receives its own invoice number, stock movements and payment allocations.
- Any price difference is handled through payment/refund allocations; no credit balance is created.

## 8. Payment, refund and loyalty boundaries

- SalePaymentAllocations references Payments transactions but does not own provider attempts or provider confirmation.
- A completed POS sale earns loyalty points only after sale completion and confirmed payment; loyalty ledger owns the points posting.
- A return causes loyalty reversals/adjustments in CRM according to the returned portion/value and approved rules.
- Refunds are separate Payments records linked to the original invoice/return; cumulative successful refund cannot exceed amount successfully paid.
- A return may have no refund or one/more partial refund records. Payment/refund status never overwrites return inspection/disposition status.
- Cash refunds are reflected in CashDrawerTransactions; electronic refunds are processed by the original payment provider where possible.

## 9. Requirement traceability

| Approved requirement/decision | Schema support |
|---|---|
| POS-01, POS-14, POS-15 | CashierShifts, CashDrawerTransactions, CashDrawerReconciliations |
| POS-06, POS-07 | SuspendedSales and SuspendedSaleLines; reprice on resume |
| POS-09, P11 | SalePaymentAllocations with full-total confirmation and split tender |
| POS-10, POS-11 | InvoiceSequences, SequenceYear, unique InvoiceNumber |
| POS-12 | InvoiceCancellationRequests and Central decisions for paid cancellation |
| POS-13 | InvoicePrintRecords |
| POS-17, POS-19 | Batch issue/Inventory integration; available sellable stock only |
| POS-18 | Immutable invoice and line/discount snapshots |
| POS-20 | WeightSource and manual-entry actor snapshot |
| PMT-06–PMT-16, RET-02–RET-07 | SalesReturns, lines, batch allocations, inspections, dispositions, exception decisions and exchanges |
| GAP-01–GAP-03 | Batch cost snapshots, composite component issue, quantity precision and amount rounding |
| GAP-08 | Pending inspection, quarantine, Central disposal approval and Branch execution evidence |
| EXC-01 | ExchangeLinks with separate replacement invoice |

## 10. Technical risks for review

1. Confirm the exact business sequence for assigning invoice numbers when a payment attempt fails; uniqueness and non-reuse are mandatory, but gap policy is not specified.
2. Define atomic completion boundary across invoice, batch issues, inventory movements, payment allocations and Outbox.
3. Define return allocation when the original invoice line was fulfilled from multiple batches and some of those batches have since been depleted or transferred.
4. Define return inspection split quantities when part of one returned line is sellable and part quarantined.
5. Define paid invoice cancellation compensation, payment reversal and inventory reversal contracts; no distributed transaction is assumed.
6. Define the local representation of Central cancellation/exception decisions and recovery when a Branch is offline.
7. Define print audit retention and invoice document rendering/version policy.
8. Confirm whether a cashier shift can be open on more than one POS device; the database constraint must match the approved session/device operating model.

## 11. Approval boundary and next step

TD-20 specifies POS, Sales, Returns and Exchanges only. It does not create SQL tables, EF Core entities, DDL or migrations. The next detailed group is Payments & Refunds, followed by Online Orders, Customer/Loyalty/Fulfillment, Expenses/Maintenance, Alerts/Audit and Reporting.

**No SQL table, EF Core entity, DDL or migration is created by this document.**
