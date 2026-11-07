# TD-17 — Detailed Logical Schema: Suppliers and Procurement

**Version:** 0.1  
**Status:** Proposed for owner review  
**Date:** 2026-11-07  
**Scope:** Central supplier master and purchasing governance; branch-originated needs requests and receiving evidence; purchase orders; supplier invoices and three-way matching; supplier returns and compensation.  
**Depends on:** Approved BRS Unit 03, approved PUR-01–PUR-11 decisions, TD-03, TD-04, TD-13 and TD-14.

> This is a logical/physical table specification for review. It is not executable DDL, EF Core source code or a migration. It does not introduce a general ledger, accounts-payable subledger, credit sales, or a new business approval policy.

## 1. Shared conventions

- Every table PK is client-generated sequential `Guid` / SQL Server `uniqueidentifier`; no integer identity PK.
- All local FKs are Guid and remain within one database. Cross-site IDs are GUID correlations only.
- UTC instants use `datetime2(3)`; quantities use `decimal(18,3)`; JOD monetary values use `decimal(18,3)` unless a later currency-specific rule requires a different scale.
- Mutable workflow rows use `rowversion`; immutable events preserve actor and timestamp.
- Financial and receiving snapshots are retained; later supplier/product price edits do not rewrite purchase orders, receipts, invoices, batches or historical returns.
- Status values use stable `tinyint` constants; localized status text is presentation-only.
- Supplier, purchasing and approval decisions are Central-owned. Branches record only their permitted request, receipt, invoice-registration and physical return evidence.

## 2. Database placement

| Table group | Central DB | Each Branch DB |
|---|---|---|
| Suppliers, contacts, terms, supplier return terms, supplier product identifiers/prices | Authoritative | Approved read-only supplier/product reference projection only when required |
| Purchase requests and lines | Central workflow copy | Branch-originating request copy |
| Purchase request decisions and decision lines | Authoritative | No |
| Purchase orders and lines | Authoritative | Read-only approved order/receiving task projection as needed |
| Purchase receipts and lines | Synchronized evidence/projection | Authoritative physical receiving record |
| Purchase receipt decisions and decision lines | Authoritative | No; approved decision is returned by command |
| Supplier invoices and lines | Central approval copy | Branch registration/origin copy |
| Invoice matching, discrepancies, due dates and supplier payments | Authoritative | No |
| Supplier returns and lines | Central approval/workflow copy | Branch request/preparation evidence copy |
| Supplier return events | Synchronized history | Physical branch execution facts |
| Supplier return settlements | Authoritative | No |

For Branch-originated records, the same request/invoice/return GUID is preserved when the record is synchronized to Central. Central decision records do not create cross-database FKs.

## 3. Supplier master and supplier terms

### 3.1 `procurement.Suppliers` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierId | uniqueidentifier | No | PK; sequential Guid |
| SupplierCode | nvarchar(40) | No | Unique stable code |
| LegalName | nvarchar(200) | No | Supplier legal/display name |
| ContactEmail | nvarchar(254) | Yes | Main contact email |
| ContactPhone | nvarchar(32) | Yes | Main contact phone |
| Address | nvarchar(500) | Yes | Address text |
| SupplierStatus | tinyint | No | Pending / Approved / Suspended |
| ApprovedAtUtc | datetime2(3) | Yes | UTC |
| ApprovedByUserId | uniqueidentifier | Yes | Central approver |
| SuspendedAtUtc | datetime2(3) | Yes | UTC |
| SuspendedByUserId | uniqueidentifier | Yes | Central actor |
| SuspensionReason | nvarchar(500) | Yes | Required when suspended |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor |
| UpdatedAtUtc | datetime2(3) | Yes | UTC |
| UpdatedByUserId | uniqueidentifier | Yes | Central actor |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique SupplierCode.
- Check SupplierStatus against stable values.
- New purchase orders are blocked unless SupplierStatus is Approved.
- Suspension prevents new purchase orders; historical orders, receipts, invoices and returns remain accessible.
- No physical deletion after a supplier is referenced by a transaction.

### 3.2 `procurement.SupplierContacts` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierContactId | uniqueidentifier | No | PK; sequential Guid |
| SupplierId | uniqueidentifier | No | FK → Suppliers |
| ContactName | nvarchar(150) | No | Contact person |
| Position | nvarchar(100) | Yes | Optional position |
| Phone | nvarchar(32) | Yes | Contact phone |
| Email | nvarchar(254) | Yes | Contact email |
| IsPrimary | bit | No | Primary contact marker |
| IsActive | bit | No | Deactivation only |
| CreatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- FK Supplier; restrict delete.
- Index SupplierId + IsActive.
- At most one active primary contact per supplier using a filtered unique index.

### 3.3 `procurement.SupplierProductTerms`, `SupplierProductPrices` and `SupplierProductIdentifiers` — Central

**`SupplierProductTerms`** stores supplier/product-specific supply conditions only where used; it must not duplicate the supplier's return terms.

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierProductTermId | uniqueidentifier | No | PK; sequential Guid |
| SupplierId | uniqueidentifier | No | FK → Suppliers |
| ProductId | uniqueidentifier | No | Central Product GUID reference |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| TermsPayload | nvarchar(max) | Yes | Structured supplier/product supply terms, schema-validated |
| IsActive | bit | No | Current record |
| RowVersion | rowversion | No | Concurrency token |

**`SupplierProductPrices`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierProductPriceId | uniqueidentifier | No | PK; sequential Guid |
| SupplierId | uniqueidentifier | No | FK → Suppliers |
| ProductId | uniqueidentifier | No | Central Product GUID reference |
| ProductUnitId | uniqueidentifier | No | Central ProductUnit GUID reference |
| CurrencyCode | char(3) | No | ISO currency code; JOD for current project |
| UnitPurchasePrice | decimal(18,3) | No | Supplier's quoted unit price |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| IsActive | bit | No | Current quote status |
| RowVersion | rowversion | No | Concurrency token |

**`SupplierProductIdentifiers`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierProductIdentifierId | uniqueidentifier | No | PK; sequential Guid |
| SupplierId | uniqueidentifier | No | FK → Suppliers |
| ProductId | uniqueidentifier | No | Central Product GUID reference |
| ProductUnitId | uniqueidentifier | Yes | Optional selling/package unit reference |
| SupplierProductCode | nvarchar(80) | Yes | Supplier's own product code |
| SupplierBarcode | nvarchar(64) | Yes | Supplier-provided barcode when available |
| IsActive | bit | No | Deactivation only |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique SupplierId + ProductId + EffectiveFromUtc for terms/prices as applicable.
- Unit price must be non-negative; currency code is validated.
- At least one of SupplierProductCode or SupplierBarcode is required. Supplier product identifiers are unique within the supplier for each non-null code/barcode.
- A product may have multiple suppliers; each supplier may provide multiple products.
- Supplier price is a supplier quote, not actual inventory cost. Actual received batch cost is captured in Branch InventoryBatches and never averaged.

### 3.4 `procurement.SupplierReturnTerms` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierReturnTermId | uniqueidentifier | No | PK; sequential Guid |
| SupplierId | uniqueidentifier | No | FK → Suppliers |
| Version | int | No | Positive version per supplier |
| AcceptedReasons | nvarchar(max) | Yes | Structured list of accepted return reasons |
| ReturnWindowDays | int | Yes | Supplier-agreed time window, when specified |
| GoodsConditionRequirements | nvarchar(1000) | Yes | Condition requirements |
| PackagingRequirements | nvarchar(1000) | Yes | Packaging requirements |
| ExpiryRequirements | nvarchar(1000) | Yes | Expiry-related conditions |
| TransportCostResponsibility | tinyint | Yes | Supplier / Supermarket / AgreedPerCase |
| CompensationMethod | tinyint | Yes | Refund / Replacement / CreditNote / AgreedPerCase |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| RecordedAtUtc | datetime2(3) | No | UTC |
| RecordedByUserId | uniqueidentifier | No | Central actor |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique SupplierId + Version.
- Check Version > 0 and ReturnWindowDays is null or non-negative.
- Validate AcceptedReasons as structured JSON. Missing terms do not block a return request. Central must verify terms and decide before approval.
- Preserve prior term versions so a later supplier agreement does not rewrite an existing return.

## 4. Branch purchase needs and Central decisions

### 4.1 `procurement.PurchaseRequests` and `PurchaseRequestLines`

The Branch creates the request-origin copy; Central receives the same GUID and owns the workflow decision.

**`PurchaseRequests`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PurchaseRequestId | uniqueidentifier | No | PK; generated at Branch and preserved centrally |
| SourceBranchId | uniqueidentifier | No | Branch identity; FK only in Central copy |
| RequestedByEmployeeId | uniqueidentifier | No | Employee GUID; FK only where employee projection exists locally, otherwise Central FK |
| SubmittedAtUtc | datetime2(3) | No | UTC |
| RequestReason | nvarchar(500) | Yes | Optional reason |
| RequestStatus | tinyint | No | Submitted / UnderReview / Approved / Rejected / ReturnedForCorrection |
| ReceivedAtCentralUtc | datetime2(3) | Yes | Central receipt time |
| RowVersion | rowversion | No | Concurrency token |

**`PurchaseRequestLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PurchaseRequestLineId | uniqueidentifier | No | PK; stable across Branch/Central copy |
| PurchaseRequestId | uniqueidentifier | No | FK to local request copy |
| ProductId | uniqueidentifier | No | Product GUID reference |
| ProductUnitId | uniqueidentifier | No | Selling/purchase unit GUID reference |
| RequestedQuantity | decimal(18,3) | No | Positive quantity |
| LineReason | nvarchar(500) | Yes | Optional reason |
| RowVersion | rowversion | No | Concurrency token |

### 4.2 `procurement.PurchaseRequestDecisions` and `PurchaseRequestDecisionLines` — Central

**`PurchaseRequestDecisions`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PurchaseRequestDecisionId | uniqueidentifier | No | PK; sequential Guid |
| PurchaseRequestId | uniqueidentifier | No | FK → Central PurchaseRequests |
| Decision | tinyint | No | Approve / Reject / ReturnForCorrection |
| Reason | nvarchar(1000) | No | Required decision reason |
| DecidedByUserId | uniqueidentifier | No | Central actor |
| DecidedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**`PurchaseRequestDecisionLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PurchaseRequestDecisionLineId | uniqueidentifier | No | PK; sequential Guid |
| PurchaseRequestDecisionId | uniqueidentifier | No | FK → PurchaseRequestDecisions |
| PurchaseRequestLineId | uniqueidentifier | No | Logical reference to request line; same Central copy |
| ApprovedQuantity | decimal(18,3) | Yes | Required for approved/adjusted line; may be null for rejected line |
| LineDecision | tinyint | No | Approve / Reject / Adjust |
| Reason | nvarchar(500) | Yes | Optional line reason |

**Constraints and indexes**
- Request → Lines is 1:N; request must contain at least one line before submission.
- Request → Decisions is 1:N append-only history.
- Decision → DecisionLines is 1:N.
- Approved quantities cannot exceed business-approved limits; values must be non-negative and unit-compatible.
- A PurchaseOrder may reference the approved request, but Central may create a purchase order without a request when the approved workflow permits it.

## 5. Central purchase orders

### 5.1 `procurement.PurchaseOrders` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PurchaseOrderId | uniqueidentifier | No | PK; sequential Guid |
| PurchaseOrderNumber | nvarchar(40) | No | Unique human-readable identifier |
| SupplierId | uniqueidentifier | No | FK → Suppliers |
| PurchaseRequestId | uniqueidentifier | Yes | Optional FK → PurchaseRequests |
| OrderDate | date | No | Business date |
| ExpectedDeliveryDate | date | Yes | Expected date if known |
| PurchaseOrderStatus | tinyint | No | Draft / Approved / PartiallyReceived / FullyReceived / Cancelled |
| CurrencyCode | char(3) | No | Currency |
| Subtotal | decimal(18,3) | No | Snapshot total |
| CreatedByUserId | uniqueidentifier | No | Central creator |
| CreatedAtUtc | datetime2(3) | No | UTC |
| ApprovedByUserId | uniqueidentifier | Yes | Central approver |
| ApprovedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

### 5.2 `procurement.PurchaseOrderLines` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PurchaseOrderLineId | uniqueidentifier | No | PK; sequential Guid |
| PurchaseOrderId | uniqueidentifier | No | FK → PurchaseOrders |
| ProductId | uniqueidentifier | No | Central product reference |
| ProductUnitId | uniqueidentifier | No | Central product unit reference |
| SupplierProductCodeSnapshot | nvarchar(80) | Yes | Supplier's code at order time |
| OrderedQuantity | decimal(18,3) | No | Positive quantity |
| UnitPurchasePrice | decimal(18,3) | No | Price snapshot |
| LineTotal | decimal(18,3) | No | Snapshot total |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique PurchaseOrderNumber.
- PurchaseOrder → PurchaseOrderLines is 1:N.
- FKs to Supplier and PurchaseRequest (optional); product/unit GUIDs reference Central catalogue.
- Order cannot be approved for an unapproved/suspended supplier.
- Purchase order and line snapshots are immutable after approval except through a controlled Central amendment workflow with audit.
- Receipt status is derived from cumulative approved/received quantities; do not infer full receipt from a single branch event.

## 6. Branch purchase receiving and Central excess approval

### 6.1 `procurement.PurchaseReceipts` — Branch execution; Central projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PurchaseReceiptId | uniqueidentifier | No | PK; generated at Branch and preserved centrally |
| PurchaseOrderId | uniqueidentifier | Yes | Central PO GUID; no Branch FK |
| SupplierId | uniqueidentifier | No | Central supplier GUID; local reference only |
| SourceBranchId | uniqueidentifier | No | Branch identity |
| IsExceptionalReceipt | bit | No | True only with prior Central exception approval |
| ExceptionalApprovalId | uniqueidentifier | Yes | Central decision reference; no cross-db FK |
| ExceptionalReason | nvarchar(500) | Yes | Required for exceptional receipt |
| ReceivedAtUtc | datetime2(3) | No | UTC |
| ReceivedByEmployeeId | uniqueidentifier | No | Branch employee GUID |
| ReceiptStatus | tinyint | No | Draft / Submitted / PartiallyApproved / Approved / Rejected |
| SubmittedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

### 6.2 `procurement.PurchaseReceiptLines` — Branch execution; Central projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PurchaseReceiptLineId | uniqueidentifier | No | PK; stable across Branch/Central copy |
| PurchaseReceiptId | uniqueidentifier | No | FK to local receipt |
| PurchaseOrderLineId | uniqueidentifier | Yes | Central PO line GUID; no Branch FK |
| ProductId | uniqueidentifier | No | Product GUID reference |
| ProductUnitId | uniqueidentifier | No | Unit GUID reference |
| OrderedQuantitySnapshot | decimal(18,3) | Yes | PO quantity where linked |
| ReceivedQuantity | decimal(18,3) | No | Actual physical received quantity |
| AcceptedQuantity | decimal(18,3) | No | Quantity currently approved for inventory |
| ExcessQuantity | decimal(18,3) | No | Quantity above order quantity, pending or decided separately |
| UnitCost | decimal(18,3) | No | Actual cost for this received batch |
| ExpiryDate | date | Yes | Required only when Product.RequiresExpiryTracking |
| InventoryBatchId | uniqueidentifier | Yes | Branch InventoryBatch GUID created for accepted quantity; local FK to the Branch batch where present |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Receipt → ReceiptLines is 1:N.
- PurchaseOrderLineId is optional only for centrally approved exceptional receiving.
- ReceivedQuantity > 0; AcceptedQuantity >= 0; ExcessQuantity >= 0; AcceptedQuantity + pending/unaccepted amount must not exceed ReceivedQuantity.
- AcceptedQuantity cannot exceed ReceivedQuantity. Excess is not sellable until Central approves it.
- ExpiryDate is required for expiry-tracked products and must be valid; it is not required for other products.
- Each distinct batch cost is preserved independently; no weighted-average costing.
- Accepted quantities create/affect Branch InventoryBatches and InventoryMovements within the Branch database transaction. Module boundaries are coordinated through application contracts, not direct module table access.

### 6.3 `procurement.PurchaseReceiptDecisions` and `PurchaseReceiptDecisionLines` — Central

**`PurchaseReceiptDecisions`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PurchaseReceiptDecisionId | uniqueidentifier | No | PK; sequential Guid |
| PurchaseReceiptId | uniqueidentifier | No | Central receipt copy GUID |
| Decision | tinyint | No | Approve / Reject / ReturnForCorrection |
| DecisionType | tinyint | No | ExceptionalReceipt / ExcessQuantity / ReceivingDifference |
| Reason | nvarchar(1000) | No | Required Central reason |
| DecidedByUserId | uniqueidentifier | No | Central actor |
| DecidedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**`PurchaseReceiptDecisionLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PurchaseReceiptDecisionLineId | uniqueidentifier | No | PK; sequential Guid |
| PurchaseReceiptDecisionId | uniqueidentifier | No | FK → PurchaseReceiptDecisions |
| PurchaseReceiptLineId | uniqueidentifier | No | Central receipt-line GUID |
| ApprovedExcessQuantity | decimal(18,3) | No | Approved excess quantity; zero if rejected |
| Decision | tinyint | No | Approve / Reject |
| Reason | nvarchar(500) | Yes | Optional line-level reason |

**Constraints and indexes**
- Receipt decision → decision lines is 1:N.
- Central approval is required before excess quantity is added to sellable stock.
- A branch applies an authenticated approval command and records the resulting stock movement locally. Central does not directly update the Branch database.

## 7. Supplier invoices, three-way match and payments

### 7.1 `procurement.SupplierInvoices` and `SupplierInvoiceLines`

Branch registers the invoice; Central owns the match review and approval.

**`SupplierInvoices`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierInvoiceId | uniqueidentifier | No | PK; generated at Branch and preserved centrally |
| SupplierId | uniqueidentifier | No | Supplier GUID |
| SourceBranchId | uniqueidentifier | No | Branch identity |
| SupplierInvoiceNumber | nvarchar(80) | No | Supplier's invoice reference |
| InvoiceDate | date | No | Supplier invoice date |
| CurrencyCode | char(3) | No | Currency |
| Subtotal | decimal(18,3) | No | Snapshot amount |
| TotalAmount | decimal(18,3) | No | Snapshot total |
| InvoiceStatus | tinyint | No | Registered / Matching / Discrepancy / Approved / Rejected |
| RegisteredByEmployeeId | uniqueidentifier | No | Branch employee |
| RegisteredAtUtc | datetime2(3) | No | UTC |
| ApprovedByUserId | uniqueidentifier | Yes | Central approver |
| ApprovedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

**`SupplierInvoiceLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierInvoiceLineId | uniqueidentifier | No | PK; stable across Branch/Central copy |
| SupplierInvoiceId | uniqueidentifier | No | FK to local invoice |
| ProductId | uniqueidentifier | No | Product GUID reference |
| ProductUnitId | uniqueidentifier | No | Unit GUID reference |
| InvoicedQuantity | decimal(18,3) | No | Quantity on supplier invoice |
| UnitPrice | decimal(18,3) | No | Invoiced unit price |
| LineAmount | decimal(18,3) | No | Invoice line amount |
| RowVersion | rowversion | No | Concurrency token |

### 7.2 `procurement.SupplierInvoiceMatchLines` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierInvoiceMatchLineId | uniqueidentifier | No | PK; sequential Guid |
| SupplierInvoiceLineId | uniqueidentifier | No | FK → Central SupplierInvoiceLines |
| PurchaseOrderLineId | uniqueidentifier | No | FK → PurchaseOrderLines |
| PurchaseReceiptLineId | uniqueidentifier | No | FK to the Central synchronized PurchaseReceiptLines projection; the original Branch row is correlated by the same GUID |
| MatchedQuantity | decimal(18,3) | No | Portion matched to this receipt/PO line |
| MatchedUnitPrice | decimal(18,3) | No | Matched price basis |
| MatchStatus | tinyint | No | Matched / QuantityMismatch / PriceMismatch / OtherMismatch |
| RowVersion | rowversion | No | Concurrency token |

### 7.3 `procurement.SupplierInvoiceDiscrepancies` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierInvoiceDiscrepancyId | uniqueidentifier | No | PK; sequential Guid |
| SupplierInvoiceId | uniqueidentifier | No | FK → SupplierInvoices |
| SupplierInvoiceLineId | uniqueidentifier | Yes | Optional FK → SupplierInvoiceLines |
| DiscrepancyType | tinyint | No | Quantity / Price / MissingReceipt / Other |
| ExpectedValue | nvarchar(300) | Yes | Safe expected-value snapshot |
| ActualValue | nvarchar(300) | Yes | Safe actual-value snapshot |
| Description | nvarchar(1000) | No | Discrepancy details |
| ResolutionStatus | tinyint | No | Open / AcceptedByCentral / Rejected / Resolved |
| ResolvedByUserId | uniqueidentifier | Yes | Central reviewer |
| ResolvedAtUtc | datetime2(3) | Yes | UTC |
| ResolutionReason | nvarchar(1000) | Yes | Central decision reason |

### 7.4 `procurement.SupplierInvoiceDueDates` and `SupplierPayments` — Central

**`SupplierInvoiceDueDates`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierInvoiceDueDateId | uniqueidentifier | No | PK; sequential Guid |
| SupplierInvoiceId | uniqueidentifier | No | FK → SupplierInvoices |
| DueDate | date | No | Contractual due date |
| DueAmount | decimal(18,3) | No | Amount due at this date |
| RowVersion | rowversion | No | Concurrency token |

**`SupplierPayments`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierPaymentId | uniqueidentifier | No | PK; sequential Guid |
| SupplierInvoiceId | uniqueidentifier | No | FK → SupplierInvoices |
| PaymentReference | nvarchar(100) | No | Unique payment/bank reference where supplied |
| PaymentAmount | decimal(18,3) | No | Positive payment amount |
| CurrencyCode | char(3) | No | Currency |
| PaymentMethod | tinyint | No | Approved payment method |
| PaymentStatus | tinyint | No | Approved / Executed / Failed / Reversed |
| ApprovedByUserId | uniqueidentifier | No | Central approver |
| ApprovedAtUtc | datetime2(3) | No | UTC |
| ExecutedByUserId | uniqueidentifier | No | Central executor |
| ExecutedAtUtc | datetime2(3) | Yes | UTC when executed |
| ExternalReference | nvarchar(150) | Yes | Bank/provider reference |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- SupplierInvoice → InvoiceLines is 1:N.
- Invoice line → match lines is 1:N; one invoice line may be reconciled against multiple partial receipts.
- Each match line references one approved PO line and one actual receipt line. Three-way match is Invoice ↔ PO ↔ Receipt.
- Any mismatch blocks invoice approval until Central records a decision.
- Due-date rows drive central reminders before due date, at due date and after overdue. The approved due schedule must be reconciled to the approved invoice total before finalizing the invoice.
- SupplierPayments are created, approved and executed by Central only. No Branch payment authority.
- Cumulative successful supplier payments must not exceed the approved invoice amount; partial payments are supported and enforced transactionally.
- These records track supplier obligations and settlement only; they do not create a general ledger or accounts-payable subledger.

## 8. Supplier returns and compensation

### 8.1 `procurement.SupplierReturns` and `SupplierReturnLines`

Central owns approval and supplier communication; Branch prepares and physically hands over the approved goods.

**`SupplierReturns`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierReturnId | uniqueidentifier | No | PK; generated at origin and preserved centrally |
| SupplierId | uniqueidentifier | No | Supplier GUID |
| SourceBranchId | uniqueidentifier | No | Branch identity |
| PurchaseReceiptId | uniqueidentifier | No | Original receiving GUID |
| ReturnStatus | tinyint | No | Requested / UnderReview / Approved / Rejected / Prepared / HandedOver / Settled / Closed |
| RequestedByEmployeeId | uniqueidentifier | No | Branch employee |
| RequestedAtUtc | datetime2(3) | No | UTC |
| ApprovedByUserId | uniqueidentifier | Yes | Central approver |
| ApprovedAtUtc | datetime2(3) | Yes | UTC |
| CentralSupplierAgreementReference | nvarchar(200) | Yes | Supplier approval/communication reference |
| RowVersion | rowversion | No | Concurrency token |

**`SupplierReturnLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierReturnLineId | uniqueidentifier | No | PK; stable across Branch/Central copy |
| SupplierReturnId | uniqueidentifier | No | FK to local return |
| PurchaseReceiptLineId | uniqueidentifier | No | Original receipt line GUID |
| InventoryBatchId | uniqueidentifier | No | Branch batch GUID; local FK to the Branch batch and to a Central inventory projection only where that projection exists |
| ProductId | uniqueidentifier | No | Product GUID |
| ProductUnitId | uniqueidentifier | No | Unit GUID |
| ReturnQuantity | decimal(18,3) | No | Positive quantity |
| UnitCostSnapshot | decimal(18,3) | No | Original batch cost |
| ReturnReason | tinyint | No | Approved reason code |
| RowVersion | rowversion | No | Concurrency token |

### 8.2 `procurement.SupplierReturnEvents` — Branch facts; Central synchronized history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierReturnEventId | uniqueidentifier | No | PK; sequential Guid |
| SupplierReturnId | uniqueidentifier | No | Logical GUID correlation; local FK only within same copy |
| EventType | tinyint | No | Prepared / HandedOver / SupplierResponse / Completed |
| ActorEmployeeId | uniqueidentifier | No | Employee GUID |
| OccurredAtUtc | datetime2(3) | No | UTC |
| Notes | nvarchar(1000) | Yes | Event notes |
| DocumentReference | nvarchar(500) | Yes | Protected file reference |
| RowVersion | rowversion | No | Concurrency token |

### 8.3 `procurement.SupplierReturnSettlements` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierReturnSettlementId | uniqueidentifier | No | PK; sequential Guid |
| SupplierReturnId | uniqueidentifier | No | FK → SupplierReturns |
| CompensationMethod | tinyint | No | Refund / Replacement / CreditNote / AgreedPerCase |
| SettlementAmount | decimal(18,3) | Yes | Required when method has monetary amount |
| SupplierReference | nvarchar(150) | Yes | Supplier refund/credit/replacement reference |
| SettledAtUtc | datetime2(3) | Yes | UTC |
| ConfirmedByUserId | uniqueidentifier | Yes | Central actor |
| SettlementStatus | tinyint | No | Pending / Confirmed / Failed |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- SupplierReturn → Lines is 1:N.
- Every return line traces to its original receiving/batch and cannot exceed quantity available for return.
- No return execution begins before Central approval and confirmation of applicable supplier terms.
- Branch records preparation and physical handover; Central records supplier communication, final review and compensation settlement.
- Inventory effect is applied to the Branch stock ledger only when the physical handover is recorded and the approved process authorizes the stock issue.
- Compensation is recorded as a supplier return settlement, not as an accounts-payable ledger entry.

## 9. Requirement traceability

| Approved requirement/decision | Schema support |
|---|---|
| BRS-PUR-001–005 / PUR-01 | Suppliers, SupplierContacts, SupplierReturnTerms and central approval/status |
| BRS-PUR-006–010 | PurchaseRequests, lines, decisions, PurchaseOrders and lines |
| BRS-PUR-011 | Exceptional receipt flag/reason and Central PurchaseReceiptDecisions |
| BRS-PUR-012–019 | PurchaseReceipts, receipt lines, per-line excess decisions and batch links |
| BRS-PUR-020–025 / RET-01 | SupplierReturns, lines, events, settlements and original receipt/batch links |
| PUR-06–PUR-08 | SupplierInvoices, three-way match, Central approval and SupplierPayments |
| PUR-09–PUR-11 | Discrepancy, invoice registration and due-date data used by centralized alerts |
| PUR-02–PUR-03 / SUPP-01–SUPP-03 | SupplierProductPrices, SupplierProductIdentifiers and supplier relationships |

## 10. Technical risks for review

1. Confirm supplier code and supplier invoice-number uniqueness scope; this proposal assumes SupplierCode is global and invoice number is unique per supplier.
2. Confirm whether one invoice line can be split across multiple receipt lines; this proposal supports it to handle partial receipts.
3. Confirm currency/rounding treatment for supplier invoices; JOD uses three decimal places in the system, but external supplier currency remains explicit.
4. Confirm whether supplier payment may cover multiple invoices in one bank transfer. This proposal links each payment to one invoice; if multi-invoice settlement is required, add a payment header and allocation child table.
5. Confirm the exact fields needed for supplier return terms and whether any terms vary by product in addition to supplier.
6. Confirm how exceptional receipt approval is represented before physical receiving begins; the approval ID must be verifiable by Branch before recording an exceptional receipt.
7. Confirm inventory batch creation/approval transaction with the Inventory module, especially accepted vs excess quantities.
8. Confirm retention of supplier invoice scans and supporting documents; store protected file references, not unrestricted public URLs.

## 11. Approval boundary and next step

TD-17 specifies Supplier & Procurement only. It does not create SQL tables, EF Core entities, DDL or migrations. The next detailed group is Inventory & Stock, including balances, movements, batch issue, reservations, stocktake freeze/close, transfers, quarantine, disposal and inventory reconciliation.

**No SQL table, EF Core entity, DDL or migration is created by this document.**
