# TD-32C — Central Suppliers, Purchasing and Receiving Physical Schema Decisions

**Version:** 0.1  
**Status:** Delegated design approval — approved for physical mapping.  
**Date:** 2026-11-28  
**Depends on:** TD-17, TD-18, TD-26, TD-30, TD-31 and TD-32.

## 1. Scope

This supplement finalizes supplier master, purchase requests, purchase orders, receiving evidence, supplier invoice matching, supplier returns and supplier payment allocation contracts. It applies the owner-approved supplier decisions and resolves the procurement risks in TD-17. It does not create a general ledger or accounts-payable subledger.

## 2. Supplier master — approved decisions

1. `SupplierCode` is centrally assigned, globally unique and immutable after the supplier is used operationally.
2. Contacts are stored in `SupplierContacts`; at most one active primary contact per supplier is enforced by a filtered unique index.
3. `SupplierProductTerms` is recorded only when product-specific supply terms exist; it does not duplicate general return terms.
4. A product may have multiple suppliers. Supplier prices are effective-dated and a PO retains its own price/code snapshot.
5. Supplier product codes and supplier barcodes are unique within the supplier, not globally across suppliers. At least one identifier must be present.
6. `SupplierReturnTerms` is supplier-level, versioned and effective-dated. A later version never rewrites a previously approved return. Case-specific exceptions are recorded on the Central return decision.
7. Supplier status values: Pending=1, Approved=2, Suspended=3. Only Approved suppliers may receive new POs. Referenced suppliers are deactivated/suspended, never deleted.

## 3. Purchase requests and purchase orders

### 3.1 Purchase requests
- A Branch request may contain multiple product lines.
- The Branch creates the request and line GUIDs; Central preserves the same IDs in its workflow copy.
- Central decisions are append-only and may approve, reject or return the request for correction; per-line quantities/adjustments are recorded.
- Approval of a request does not create a PO automatically. Central procurement staff decide supplier sourcing and create POs separately.
- A single request may be fulfilled by multiple POs, normally one PO per selected supplier.

### 3.2 Purchase orders
- One PO belongs to exactly one supplier. A PO cannot contain lines sourced from different suppliers.
- A supplier may have many POs; a request may be linked to multiple POs. A PO may be created without a request only through an authorized Central workflow.
- A PO is not approved unless the supplier status is Approved at the approval transaction.
- Approved PO snapshots are immutable for ordinary editing. Changes use an explicit Central amendment operation with a monotonically increasing RevisionNumber, rowversion concurrency and an audit before/after record.
- After any quantity has been received, amendments may affect only the remaining unreceived quantity/value. Received quantities, receipt facts, batch costs and invoice matches are never rewritten.
- Branch receives the approved PO revision as a read-only projection. A stale revision cannot authorize new receiving after a newer approved revision is applied.
- PO number is globally unique in Central. Order status is derived from approved/cumulative receipt quantities and explicit cancellation state, not a manually editable status.

## 4. Receiving and exception approval

1. Partial receiving is allowed. Multiple receipt headers may refer to the same PO and PO line over time.
2. A normal receipt must reference an Approved PO and a line belonging to that PO. The Branch validates the PO revision and remaining receivable quantity.
3. Exceptional receipt (no approved PO) requires a Central approval before physical acceptance is posted to inventory. The Branch first creates a durable receipt-intent header with its GUID, supplier, branch and reason; Central records the exception decision against that stable receipt ID; only after the approved command is applied may the Branch complete physical receipt and post stock.
4. The exception approval must be valid for the same Branch and Supplier, and must be unexpired/not already consumed. A command ID and OperationId make its use idempotent.
5. Received quantities are recorded as physical facts. Accepted quantities are posted to Branch inventory in the same Branch transaction as InventoryBatch and InventoryMovement creation.
6. Any quantity above the PO's remaining ordered quantity is excess. Excess remains quarantined/non-sellable until Central approves the exact quantity; approval is applied through a durable Branch command. Central never updates Branch stock directly.
7. Rejected or unaccepted quantities do not enter sellable stock. Receipt corrections are append-only/compensating; do not erase physical receipt evidence.
8. ExpiryDate is required for products with RequiresExpiryTracking=true. Actual unit cost is retained per received batch and is not overwritten by supplier quote changes or averaged across batches.

## 5. Supplier invoice registration and three-way match

- Branch registers supplier invoice header/lines and preserves the same GUIDs in the Central approval copy.
- Supplier invoice number is unique per Supplier, across all Branches. Add a normalized invoice-number key and enforce unique `(SupplierId, NormalizedSupplierInvoiceNumber)`; original display number is retained.
- A supplier invoice cannot be approved until every material line is matched or a discrepancy has an explicit Central resolution.
- Three-way match is Invoice ↔ Approved PO ↔ Actual Receipt.
- One invoice line may be split across multiple receipt lines to support partial receiving. Each match row references one invoice line, one PO line and one receipt line.
- Match quantities cannot exceed invoice line quantity, approved PO line quantity, or the remaining accepted receipt quantity available for matching. Enforce cumulative caps transactionally.
- Duplicate invoice registration from another Branch for the same supplier is rejected centrally; it does not create a second payable.
- Supplier invoice documents use protected object-storage references and file hashes. Never store arbitrary public URLs or payment/card secrets.
- JOD is the only enabled settlement currency in the first release. CurrencyCode remains explicit in the schema for traceability; non-JOD invoice/payment settlement is blocked until an approved exchange-rate and rounding contract exists. No implicit conversion is performed.

## 6. Supplier payment header and allocations

TD-31 is authoritative and supersedes the one-payment/one-invoice model.

### 6.1 `procurement.SupplierPayments` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierPaymentId | uniqueidentifier | No | PK; sequential Guid |
| SupplierId | uniqueidentifier | No | One payment is for one supplier |
| PaymentReference | nvarchar(100) | No | Central unique reference |
| PaymentAmount | decimal(18,3) | No | Positive amount |
| CurrencyCode | char(3) | No | JOD in first release |
| PaymentMethod | tinyint | No | Approved Central payment method |
| PaymentStatus | tinyint | No | Draft / Approved / Executed / Failed / Reversed |
| ApprovedByUserId | uniqueidentifier | Yes | Required on approval |
| ApprovedAtUtc | datetime2(3) | Yes | UTC |
| ExecutedByUserId | uniqueidentifier | Yes | Required on execution |
| ExecutedAtUtc | datetime2(3) | Yes | UTC |
| ExternalReference | nvarchar(150) | Yes | Bank/provider reference |
| CreatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

### 6.2 `procurement.SupplierPaymentAllocations` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SupplierPaymentAllocationId | uniqueidentifier | No | PK; sequential Guid |
| SupplierPaymentId | uniqueidentifier | No | FK → SupplierPayments |
| SupplierId | uniqueidentifier | No | Repeated for same-supplier composite FK validation |
| SupplierInvoiceId | uniqueidentifier | No | FK → SupplierInvoices |
| AllocatedAmount | decimal(18,3) | No | Positive amount |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor |

**Constraints and transactional rules**
- One payment header may allocate to multiple invoices, but all invoices must belong to the same SupplierId as the payment.
- Unique `(SupplierPaymentId, SupplierInvoiceId)`; an invoice appears at most once per payment.
- Allocation currency equals payment/invoice currency.
- Total allocations must equal PaymentAmount before the payment can transition to Executed; Draft/Approved may be assembled before final posting.
- Sum of allocations cannot exceed PaymentAmount; each invoice's cumulative successful allocations cannot exceed its approved outstanding amount.
- Allocation posting and payment status transition occur in one Central transaction using a concurrency-controlled invoice payable balance/state. Concurrent payments cannot consume the same outstanding amount.
- A reversal is a compensating financial record/state transition; it does not delete original allocations.
- Branches cannot create, approve, execute or confirm SupplierPayments.

## 7. Supplier returns

- Return request is linked to the original receipt and exact InventoryBatch/receipt line.
- Central approves before Branch prepares/physically hands over goods.
- Return quantity cannot exceed the available returnable quantity after prior returns, sales/stock issues and other dispositions.
- Branch records physical preparation/handover as immutable events; the stock issue is committed in the Branch ledger at the approved physical handover boundary.
- Central records supplier response and settlement method (refund, replacement, credit note or approved case-specific agreement).
- Settlement is not a general ledger or AP subledger posting.

## 8. Required physical indexes and constraints

- Unique SupplierCode.
- Filtered unique active primary SupplierContact per Supplier.
- Unique SupplierProductCode and SupplierBarcode within Supplier scope when non-null.
- Unique SupplierProductPrice effective start per Supplier/Product/Unit; overlapping active periods are rejected transactionally.
- Unique PurchaseOrderNumber.
- Unique normalized SupplierInvoiceNumber per Supplier across all Branches.
- Unique (SupplierPaymentId, SupplierInvoiceId) and same-supplier composite FKs for payment allocations.
- Check positive quantities/prices/amounts where applicable; all status/type values use named CHECK constraints.
- RowVersion on mutable supplier, request, PO, receipt and payment state.
- All foreign keys remain local to the Central or Branch database. Cross-site IDs are correlation GUIDs only.

## 9. Implementation and test boundary

- Central is the authority for suppliers, request decisions, POs, invoice match, payment allocation, return approval and settlements.
- Branch is the authority for request origin, physical receipt, invoice registration origin, and physical return events.
- Test multi-PO sourcing from one request, one-supplier-per-PO, approved amendment after partial receipt, exceptional receipt before inventory posting, excess approval command replay, duplicate supplier invoice across Branches, invoice-line split match, payment across multiple same-supplier invoices, allocation overpayment race, and reversal auditability.
- This supplement does not generate EF Core mappings or migrations. They are added after the complete Central and Branch physical schema packages are reconciled.
