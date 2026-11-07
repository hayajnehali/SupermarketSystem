# TD-14 — Logical Database Relationships and Cardinalities

**Version:** 0.1  
**Status:** Proposed for owner review  
**Date:** 2026-11-07  
**Depends on:** Approved BRS, TD-03, TD-04, TD-05, TD-09, TD-12 and TD-13.  
**Purpose:** Define the first logical relationship map for the TD-13 table inventory before columns, EF Core configurations or migrations are designed.

> This document describes conceptual relationships and cardinalities. It is not SQL DDL, does not authorize cross-database foreign keys, and does not freeze every optional relationship. The owning module remains responsible for its tables.

## 1. Relationship and key rules

1. Every primary key is `Guid` in C# and SQL Server `uniqueidentifier`. No integer identity primary keys.
2. A real SQL foreign key is permitted only when both tables are in the same database. Cross-database references are GUID values validated through authenticated APIs and synchronization contracts, never SQL foreign keys.
3. A branch database is branch-scoped. It must not create a SQL FK to the Central `org.Branches` table. A branch identifier in a branch record is context/metadata, not a cross-database relationship.
4. For replicated records, the source system owns the authoritative row. A synchronized copy is a projection and must not independently mutate source-owned data.
5. Parent-to-child relationships below are normally one-to-many (1:N). Many-to-many relationships must use the named join/assignment table; do not store comma-separated IDs.
6. Historical records reference immutable transaction snapshots. They must not depend on current product/price/customer master values to reconstruct what happened.
7. Optional relationships are nullable only when the business scenario allows them (for example, an exceptional receipt may have no purchase order, or an exceptional return may have no original invoice and must be centrally referred).

## 2. Central organization and access

| Parent | Child / relationship | Cardinality | Boundary and notes |
|---|---|---|---|
| `org.Branches` | `org.EmployeeBranchAssignments` | 1:N | Central FK. |
| `org.Employees` | `org.EmployeeBranchAssignments` | 1:N | Central FK; effective dates preserve assignment history. |
| `org.Roles` | `org.RolePermissions` | 1:N | Central FK. |
| `org.Permissions` | `org.RolePermissions` | 1:N | Central FK; Roles and Permissions are M:N through RolePermissions. |
| Identity user | `org.Employees` | 1:0..1 or 1:1 | Exact direction and Identity key mapping deferred to TD-06 implementation design; do not duplicate credentials. |
| `org.Employees` | `org.EmployeePermissionGrants` | 1:N | Central FK. |
| `org.BranchAccessSnapshots` | `org.BranchAccessSnapshotEmployees` | 1:N | Branch-local FK within the same active snapshot. |
| `org.BranchAccessSnapshotEmployees` | `org.BranchAccessSnapshotPermissions` | 1:N | Branch-local FK; effective permission snapshot for one employee. |
| `org.RegisteredDevices` | Branch local device-validation snapshot | 1:0..N | Cross-database projection/reference only; no FK. |
| Central employee/role/permission records | `org.BranchAccessSnapshots` | 1:N per branch snapshot | Projection built from approved central state; local snapshot rows relate only within the Branch DB. |
| `org.RuntimePolicies` | `org.BranchRuntimePolicies` | 1:N logical publication | Centrally owned versioned policy; branch applies the authenticated local projection with no cross-database FK. |

ASP.NET Core Identity framework tables (such as user-role joins) are implementation-owned and must be mapped in the identity design rather than recreated as parallel custom tables.

## 3. Product catalog

| Parent | Child / relationship | Cardinality | Boundary and notes |
|---|---|---|---|
| `catalog.ProductCategories` | `catalog.Products` | 1:N | A product may have one assigned category unless the approved catalog rules later require multiple categorization. |
| `catalog.ProductCategories` | `catalog.ProductCategories` | 1:N | Optional parent category to child category self-reference. |
| `catalog.Brands` | `catalog.Products` | 1:N | A brand may be absent where not applicable. |
| `catalog.Countries` | `catalog.Products` | 1:N | Country of origin is an optional reference. |
| `catalog.Products` | `catalog.ProductBarcodes` | 1:N | Barcode uniqueness enforced against active product assignments. |
| `catalog.Products` | `catalog.ProductUnits` | 1:N | Product-specific approved selling/base units. |
| `catalog.ProductUnits` | `catalog.ProductUnitConversions` | 1:N | Conversion rows connect a source unit and target unit belonging to the same product; enforce uniqueness and positive precision. |
| `catalog.Products` | `catalog.CompositeProductComponents` | 1:N as composite parent | Component rows also reference a component Product; this is a product-to-product self relationship through the component table. |
| `catalog.Products` | `catalog.ProductAttributes` | 1:N | Structured product attribute values. |
| `catalog.Products` | `catalog.ProductMedia` | 1:N | Media metadata; binary content remains in managed file/object storage. |
| `catalog.ProductRequests` | `catalog.ProductRequestLines` | 1:N | Branch request origin, Central approval authority; branch-to-central relationship uses request GUID, not FK. |
| `catalog.ProductRequests` | `catalog.ProductRequestDecisions` | 1:N | Central decision history: approve, reject or return for correction. |

## 4. Suppliers and procurement

| Parent | Child / relationship | Cardinality | Boundary and notes |
|---|---|---|---|
| `procurement.Suppliers` | `procurement.SupplierContacts` | 1:N | Central FK. |
| `procurement.Suppliers` | `procurement.SupplierProductTerms` | 1:N | Central FK. |
| `procurement.Suppliers` | `procurement.SupplierProductPrices` | 1:N | Central FK; product reference is central catalog identity. |
| `catalog.Products` | `procurement.SupplierProductPrices` | 1:N | Central FK; price history/effective period. |
| `procurement.PurchaseRequests` | `procurement.PurchaseRequestLines` | 1:N | Request header and requested products. |
| `procurement.PurchaseRequests` | `procurement.PurchaseRequestDecisions` | 1:N | Central decision history: approve, reject, adjust or return for correction. |
| `procurement.Suppliers` | `procurement.PurchaseOrders` | 1:N | Central FK. |
| `procurement.PurchaseOrders` | `procurement.PurchaseOrderLines` | 1:N | Central FK. |
| `procurement.PurchaseRequests` | `procurement.PurchaseOrders` | 0..1:N | A purchase order may reference the approved need request; do not force a request link if the approved workflow permits direct central purchase. |
| `procurement.PurchaseOrders` | `procurement.PurchaseReceipts` | 1:N or 0..N | Receipt can be partial; exceptional receipt without PO is permitted only with central approval and recorded reason. |
| `procurement.PurchaseReceipts` | `procurement.PurchaseReceiptLines` | 1:N | Receipt execution is Branch-owned; Central receives synchronized projection. |
| `procurement.PurchaseOrderLines` | `procurement.PurchaseReceiptLines` | 1:N or 0..N | Optional on exceptional receipt; a receipt line may map to an approved PO line. |
| `procurement.PurchaseReceiptLines` | `inventory.InventoryBatches` | 1:N | Each accepted received batch retains its own cost and optional expiry. Excess quantity is not available until Central approval. |
| `procurement.PurchaseReceiptLines` | `procurement.PurchaseReceiptDecisions` | 1:N | Central decisions for excess/difference handling. |
| `procurement.SupplierInvoices` | `procurement.SupplierInvoiceMatchLines` | 1:N | Invoice is registered at Branch and approved at Central. |
| `procurement.PurchaseOrderLines` | `procurement.SupplierInvoiceMatchLines` | 1:N | Three-way match reference. |
| `procurement.PurchaseReceiptLines` | `procurement.SupplierInvoiceMatchLines` | 1:N | Actual received quantity reference. Match is evaluated centrally. |
| `procurement.SupplierInvoices` | `procurement.SupplierInvoiceDiscrepancies` | 1:N | Discrepancies block invoice approval until Central decision. |
| `procurement.SupplierInvoices` | `procurement.SupplierInvoiceDueDates` | 1:N | One invoice may have one or more contractual due dates/installments. |
| `procurement.SupplierInvoices` | `procurement.SupplierPayments` | 1:N | Payments are centrally approved and executed; cumulative paid amount must respect approved invoice terms. |
| `procurement.SupplierReturns` | `procurement.SupplierReturnLines` | 1:N | Return request references original supplier, receiving and batch. |
| `procurement.PurchaseReceiptLines` / `inventory.InventoryBatches` | `procurement.SupplierReturnLines` | 1:N | A return line must trace to received quantity/batch and cannot exceed quantity available for return. |

## 5. Inventory, stocktaking, transfers and disposal

| Parent | Child / relationship | Cardinality | Boundary and notes |
|---|---|---|---|
| `inventory.Warehouses` | `inventory.WarehouseLocations` | 1:N | Branch-local FK. |
| `inventory.Warehouses` | `inventory.InventoryBatches` | 1:N | Branch-local FK; each branch owns its physical batches. |
| `inventory.WarehouseLocations` | `inventory.InventoryBatches` | 1:N | Branch-local FK. |
| `catalog.Products` / `catalog.ProductUnits` | `inventory.InventoryBatches` | 1:N | Branch stores central product/unit GUID references as local projection relationships; use local FK only if local catalog projection tables are present in the same Branch DB. |
| `inventory.InventoryBatches` | `inventory.InventoryBalances` | 1:N or 1:1 by balance grain | Balance grain is product + unit + warehouse/location + batch + stock state; define unique key in physical design. |
| `inventory.InventoryMovements` | `inventory.InventoryMovementLines` | 1:N | Movement header and immutable stock effects. |
| `inventory.InventoryBatches` | `inventory.InventoryMovementLines` | 1:N | Batch cost/quantity lineage for stock-in/out. |
| `inventory.WarehouseLocations` | `inventory.InventoryMovementLines` | 1:N | Source/destination location references remain within Branch DB. |
| `online.OrderReservations` (Central) | `inventory.StockReservations` (Branch) | 1:N logical link | Cross-database correlation by reservation/order GUID; no SQL FK. Branch reservation acceptance is persisted before Central acknowledgement. |
| `inventory.StockReservations` | `inventory.InventoryBatches` | N:1 or N:M via reservation allocation | If a reservation is allocated to exact batches, use a separate allocation child table; do not assume one reservation consumes one batch. |
| `inventory.StockReservations` | `inventory.StockReservationAllocations` | 1:N | Optional exact batch allocations when required; each allocation references one local batch. |
| `inventory.ReorderPolicies` | `inventory.StockIncreaseRequests` | 1:N logical branch/product relationship | Central proactively monitors thresholds; branch requests are an additional input, not a prerequisite for replenishment. |
| `inventory.StockIncreaseRequests` | `inventory.StockIncreaseRequestDecisions` | 1:N | Branch-originated request with Central decision history. |
| `inventory.Stocktakes` | `inventory.StocktakeScopeLines` | 1:N | Central session and explicit full/partial scope. |
| `inventory.Stocktakes` | `inventory.StocktakeAssignments` | 1:N | Central employees assigned to session/scope. |
| `inventory.Stocktakes` | `inventory.StocktakeLines` | 1:N | Central blind count entries; expected quantity is hidden from counters. |
| `inventory.StocktakeLines` | `inventory.StocktakeRecounts` | 1:N | Second count and subsequent recounts are retained. |
| `inventory.StocktakeLines` | `inventory.StocktakeVarianceCases` | 0..1:1 | Create a case only when a variance exists; each product/scope variance is tracked independently. |
| `inventory.StocktakeVarianceCases` | `inventory.StocktakeVarianceDecisions` | 1:N | Central investigation and final adjustment approval/rejection. |
| `inventory.Stocktakes` | `inventory.StocktakeSessionEvents` | 1:N | Start/cancel/close/freeze-release history. |
| `inventory.Stocktakes` | `inventory.StocktakeFreezeAcknowledgements` | 1:N | Central records acknowledgement from the relevant Branch DB; freeze is applied locally for the scoped products. |
| `inventory.Stocktakes` (via central freeze command) | `inventory.StocktakeLocks` (Branch) | 1:N logical link | Branch persists the active scoped lock locally; Central releases it only after authorized session closure/cancellation. |
| `inventory.StockTransfers` | `inventory.StockTransferLines` | 1:N | Central request, approval and execution workflow. |
| `inventory.StockTransfers` | `inventory.StockTransferEvents` | 1:N | Central lifecycle and command history. |
| `inventory.StockTransfers` | `inventory.StockTransferBranchEvents` | 1:N logical link | Branch source/destination acknowledgements correlated by transfer/line GUID; no cross-database FK. |
| `inventory.InventoryQuarantines` | `inventory.InventoryDisposalRequests` | 1:N | A quarantine record may be submitted for disposal; only the approved quantity can be disposed. |
| `inventory.InventoryDisposalRequests` | `inventory.InventoryDisposalRequestLines` | 1:N | Branch request to Central. |
| `inventory.InventoryDisposalRequests` | `inventory.InventoryDisposalDecisions` | 1:N | Central approval/rejection history. |
| `inventory.InventoryDisposalRequests` | `inventory.InventoryDisposals` | 0..1:N | Physical disposal follows approval and is recorded in Branch DB. |
| `inventory.InventoryDisposals` | `inventory.InventoryDisposalLines` | 1:N | Actual batch/quantity disposed and evidence references. |

## 6. Pricing and promotions

| Parent | Child / relationship | Cardinality | Boundary and notes |
|---|---|---|---|
| `pricing.PriceVersions` | `pricing.ProductPrices` | 1:N | Central version publication. |
| `catalog.Products` | `pricing.ProductPrices` | 1:N | Central product price history; branch consumes approved versioned projection. |
| `pricing.Promotions` | `pricing.PromotionConditions` | 1:N | Central promotion definition. |
| `pricing.Promotions` | `pricing.PromotionRewards` | 1:N | Central promotion effects. |
| `pricing.Coupons` | `pricing.CouponRedemptions` | 1:N | Central coupon usage authority; branch Offline event may be pending synchronization if permitted. |
| `crm.Customers` / `online.Orders` | `pricing.CouponRedemptions` | 1:N logical references | Customer/order references are same-DB Central; branch event references are correlated without cross-db FKs. |

## 7. POS sales, returns, payments and cash shifts

| Parent | Child / relationship | Cardinality | Boundary and notes |
|---|---|---|---|
| `sales.CashierShifts` | `sales.SalesInvoices` | 1:N | Branch-local FK; each invoice belongs to the active shift that created it. |
| `sales.SalesInvoices` | `sales.SalesInvoiceLines` | 1:N | Branch-local FK; invoice line snapshots product/unit/price/discount. |
| `sales.SalesInvoiceLines` | `sales.SalesInvoiceBatchIssues` | 1:N | Exact batch costs and quantities issued to each sold line. |
| `sales.SalesInvoiceLines` | `sales.CompositeSaleComponentIssues` | 0..N | Only for composite products; each component consumption references its batch. |
| `sales.CashierShifts` | `sales.CashDrawerTransactions` | 1:N | Branch-local FK. |
| `sales.SalesInvoices` | `sales.SalePaymentAllocations` | 1:N | Split tender rows; confirmed total must equal full invoice amount. |
| `payments.PaymentTransactions` | `payments.PaymentAttempts` | 1:N | Payment attempt history is independent from invoice/order status. |
| `payments.PaymentMethods` | `sales.SalePaymentAllocations` | 1:N logical reference | Central payment-method configuration is projected to Branch; no cross-db FK if not copied as a local table. |
| `sales.SalesInvoices` | `sales.SalesReturns` | 1:N | Normal returns require original invoice. No-invoice exception is centrally referred and approved. |
| `sales.SalesReturns` | `sales.SalesReturnLines` | 1:N | Return header and returned quantities. |
| `sales.SalesInvoiceLines` | `sales.SalesReturnLines` | 1:N | Each return line references original sold line; cumulative returned quantity/weight cannot exceed eligible sold quantity. |
| `sales.SalesReturnLines` | `sales.SalesReturnInspections` | 1:N | Inspection history and responsible authorized employee. |
| `sales.SalesReturnLines` | `sales.SalesReturnDispositions` | 1:N | Return-to-stock, quarantine or other approved disposition. |
| `sales.SalesReturns` | `payments.Refunds` | 0..N | A return can have no refund, one refund, or multiple partial refund records; cumulative successful refund cannot exceed amount paid. |
| `sales.SalesReturns` | `sales.ExchangeLinks` | 0..1:N | Links the return to a separate replacement invoice; financial and stock records remain separate. |
| `sales.SalesInvoices` | `sales.InvoicePrintRecords` | 1:N | Reprint history; no new sale or stock movement. |
| `sales.InvoiceSequences` | `sales.SalesInvoices` | 1:N logical sequence scope | Branch/year sequence allocator; uniqueness is branch + SequenceYear + invoice sequence, not a PK. |

A completed POS sale, inventory movement, payment allocations and Outbox message must commit atomically inside the Branch database transaction boundary. A return/refund is not one combined status: inspection, stock disposition and monetary refund remain independently traceable.

## 8. Online orders, payments and fulfillment

| Parent | Child / relationship | Cardinality | Boundary and notes |
|---|---|---|---|
| `crm.Customers` | `online.Carts` | 1:N | Central FK; cart may be active or historical according to lifecycle policy. |
| `online.Carts` | `online.CartLines` | 1:N | Central FK. |
| `crm.Customers` | `online.Orders` | 1:N | Central FK. |
| `online.Orders` | `online.OrderLines` | 1:N | Central FK; immutable confirmed snapshots. |
| `online.Orders` | `online.OrderStatusHistory` | 1:N | Central append-only status transitions. |
| `online.Orders` | `online.OrderReservations` | 1:N | Central reservation/payment lifecycle; the 15-minute reservation and late-payment rules remain as approved. |
| `online.OrderReservations` | `inventory.StockReservations` | 1:N logical cross-site relationship | Central reservation and Branch stock reservation are correlated by GUID; Branch acceptance is authoritative for physical stock. |
| `online.Orders` | `online.OrderDeliveryDetails` | 1:1 or 0..1:1 | One delivery/pickup destination detail set per order when applicable; city/area references are central. |
| `online.DeliveryCities` | `online.DeliveryAreas` | 1:N | Central FK. |
| `online.DeliveryAreas` | `online.DeliveryFeeRules` | 1:N | Central fee history/effective periods; a free-delivery rule is represented by the approved rule/configuration. |
| `online.Orders` | `online.OrderCancellationRequests` | 0..N | Customer requests cancellation before preparation; Central decides. |
| `online.OrderCancellationRequests` | `online.OrderCancellationDecisions` | 1:N | Central decision history. |
| `online.Orders` | `payments.PaymentTransactions` | 1:N | Central online payment transactions/attempts; late payments remain linked to the cancelled original order. |
| `online.Orders` | `online.OrderNotifications` | 1:N | Central customer notifications (website and email). |
| `online.OrderNotifications` | `online.NotificationDeliveryAttempts` | 1:N | Delivery results. |
| `online.Orders` | `fulfillment.FulfillmentTasks` | 1:N | Central assignment; operational work projection at the assigned Branch. |
| `fulfillment.FulfillmentTasks` | `fulfillment.FulfillmentExceptions` | 1:N | Branch reports shortages/unavailability to Central. |
| `fulfillment.FulfillmentExceptions` | `fulfillment.FulfillmentExceptionDecisions` | 1:N | Central alone decides the order-level outcome. |
| `fulfillment.FulfillmentTasks` | `fulfillment.PickingSessions` | 1:N | Branch execution. |
| `fulfillment.PickingSessions` | `fulfillment.PickingLines` | 1:N | Picked quantities and barcode checks. |
| `fulfillment.FulfillmentTasks` | `fulfillment.DeliveryAssignments` | 0..N | Central assignment history; one active assignment at a time if policy requires. |
| `fulfillment.DeliveryAssignments` | `fulfillment.DeliveryAttempts` | 1:0..1 | The approved single customer delivery attempt is enforced by a unique constraint on the order/assignment business key. Technical retries of saving/synchronizing the same attempt do not create another attempt. |
| `fulfillment.DeliveryAttempts` | `fulfillment.DeliveryProofs` | 1:0..N | Manual confirmation/evidence references. |
| `fulfillment.FulfillmentTasks` | `fulfillment.PickupHandovers` | 0..1:N | Pickup branch execution and handover evidence. |
| `online.Orders` | `fulfillment.DeliveryDisputes` | 1:N | Central dispute records; no customer objection deadline is introduced. |
| `fulfillment.DeliveryDisputes` | `fulfillment.DeliveryDisputeDecisions` | 1:N | Central accept/reject decision history. |

## 9. Customer, loyalty and complaints

| Parent | Child / relationship | Cardinality | Boundary and notes |
|---|---|---|---|
| `crm.Customers` | `crm.CustomerPhoneVerifications` | 1:N | Central OTP attempt history; never store plaintext OTP. |
| `crm.Customers` | `crm.LoyaltyAccounts` | 1:0..1 | Unique customer loyalty account unless an approved multi-program design requires otherwise. |
| `crm.LoyaltyProgramRules` | `crm.LoyaltyPointTransactions` | 1:N logical reference | Every points posting records the applicable rule/version snapshot. |
| `crm.LoyaltyAccounts` | `crm.LoyaltyPointTransactions` | 1:N | Append-only earn/redeem/reverse/expire/adjust ledger. |
| `crm.Customers` | `crm.CustomerComplaints` | 1:N | Central FK. |
| `crm.CustomerComplaints` | `crm.ComplaintActions` | 1:N | Central action/status history. |
| `sales.SalesReturns` / `online.Orders` | `crm.LoyaltyPointTransactions` | 0..N logical references | Return reversal and online/POS earning/redemption trace to source transaction; cross-db POS event uses GUID correlation only. |

Points are credited only after completed sale and confirmed payment. Return-related reversals are calculated from the returned portion/value under the approved loyalty rules; the ledger is never rewritten in place.

## 10. Expenses, payroll tracking, deposits, maintenance, alerts and audit

| Parent | Child / relationship | Cardinality | Boundary and notes |
|---|---|---|---|
| `operations.ExpenseCategories` | `operations.ExpenseRequests` | 1:N | Central reference; Branch request carries category GUID. |
| `operations.ExpenseRequests` | `operations.ExpenseRequestDocuments` | 1:N | Branch-originated supporting evidence with Central review projection. |
| `operations.ExpenseRequests` | `operations.ExpenseApprovals` | 1:N | Central decision history. |
| `operations.ExpenseRequests` | `operations.ExpenseExecutions` | 0..1:N | Branch execution evidence after approval; Central reviews completion. |
| `operations.CashDeposits` | `operations.CashDepositLines` | 1:N | Central record; each line references source branch/shift/cash collection GUIDs without cross-database FK. |
| `operations.PayrollTrackingRecords` | Supporting payroll tables | Deferred | Do not invent supporting tables until the approved FIN-03 tracking scope is explicitly mapped. |
| `maintenance.MaintainableAssets` | `maintenance.MaintenanceRequests` | 1:N | Central asset master; Branch request refers to asset GUID. |
| `maintenance.MaintenanceRequests` | `maintenance.MaintenanceAssignments` | 1:N | Central assignment history. |
| `maintenance.MaintenanceRequests` | `maintenance.MaintenanceWorkLogs` | 1:N | Technician report/cost and Branch operational confirmation. |
| `maintenance.MaintenanceRequests` | `maintenance.MaintenanceDocuments` | 1:N | Photo/video/report metadata and protected storage reference. |
| `alerts.AlertDefinitions` | `alerts.AlertEvents` | 1:N | Central configured alert types and event instances. |
| `alerts.AlertEvents` | `alerts.AlertRecipients` | 1:N | Recipients/responsible roles for each event. |
| `alerts.AlertEvents` | `alerts.AlertDeliveries` | 1:N | In-app, email and mobile push delivery results for central alerts. |
| `alerts.AlertRecipients` | `alerts.AlertAcknowledgements` | 1:N | Acknowledgement/action notes; does not delete the alert. |
| `audit.AuditEntries` | `audit.AuditChangeDetails` | 1:N | Local database boundary; before/after details only where safe and required. |
| `audit.SecurityEvents` | `alerts.AlertEvents` | 0..N | Suspicious security events may raise central alerts; preserve event and alert as distinct records. |
| `reporting.ReportRunRecords` | Reporting read models | Derived | Read models are projections, not authoritative operational entities. |

## 11. Synchronization relationships — no cross-database FKs

The following are logical message/correlation relationships only:

- Central command `sync.BranchCommands` → Branch command receipt/processing through `sync.InboxMessages` and `sync.ProcessedOperations`.
- A committed Branch business operation → Branch `sync.OutboxMessages` → Central `sync.InboxMessages` → Central `sync.ProcessedOperations`.
- Central-to-Branch approved master-data publication → versioned local projections and `sync.SyncCheckpoints`.
- Branch-to-Central operational facts → immutable Central history/projection and acknowledgement/checkpoint.
- Reservation, stocktake freeze, stock transfer, inventory adjustment, disposal and offline-resume commands are correlated by stable GUID OperationIds and entity GUIDs; no direct SQL or distributed transaction crosses database boundaries.

TD-12 remains authoritative for the physical columns, uniqueness, indexes, leases and SQL atomic-claim behavior of the five synchronization tables.

## 12. Cross-domain transaction boundaries

| Workflow | Atomic local database boundary | Cross-site continuation |
|---|---|---|
| POS sale | Branch invoice + invoice lines + batch issues + inventory movements + payment allocations + Outbox | Central receives immutable sale/payment/stock facts idempotently. |
| Purchase receipt | Branch receipt + receipt lines + accepted batch/stock movement + Outbox | Central approves excess and receives evidence; approval command returns to Branch before excess becomes sellable. |
| Supplier invoice registration | Branch invoice registration and Outbox | Central three-way match, discrepancy decision, approval and supplier payment. |
| Stock reservation | Branch stock reservation and stock concurrency update in one Branch transaction | Central confirms order reservation only after durable Branch acknowledgement. |
| Stocktake freeze/release | Branch scoped stock-operation lock state and local acknowledgement | Central owns session/count/variance decision; authorized commands freeze/release branch scope. |
| Inventory disposal | Branch quarantine/request submission; later Branch disposal movement after central approval | Central approval is a command; Branch records actual disposal and sends immutable evidence. |
| Stock transfer | Each source/destination Branch records only its own authorized movement atomically with its Outbox | Central orchestrates both legs and reconciles acknowledgements; no distributed transaction. |
| POS return/refund | Branch return/inspection and any permitted stock disposition; local cash refund where applicable | Central approval/payment provider refund and loyalty effects continue independently; no premature stock release. |
| Online checkout | Central order + payment/reservation lifecycle; Branch stock reservation is a separate local transaction | Central waits for durable Branch reservation acceptance; payment callback is independently idempotent. |
| Online fulfillment | Branch picking/handover/delivery event + Outbox | Central order lifecycle, customer notifications and dispute decision. |
| Expense | Branch request/execution evidence; Central approval/review | No purchase/payment before central approval. |

## 13. Relationship risks to resolve before physical schema

1. Confirm whether `catalog.ProductUnits` contains the base unit and all selling units, and whether `ProductUnitConversions` stores directed or normalized conversion pairs. Do not duplicate conversion factors.
2. Define exact inventory balance grain and whether batch is mandatory for non-expiry products; preserve FIFO/FEFO issue lineage without forcing unnecessary expiry data.
3. Define stock reservation allocation to batches versus product-level quantity reservation; ensure POS and reservation updates serialize against the same Branch stock state.
4. Define purchase receipt handling for over-received quantity and its approval/activation transaction; do not count excess as sellable before central approval.
5. Define three-way invoice match tolerances and discrepancy representation as technical configuration only where already approved; no business tolerance is inferred here.
6. Define supplier payment allocation across invoices/installments and unique provider/bank references; do not introduce a general ledger.
7. Define return inspection/disposition cardinalities, cumulative returned quantity validation, weight precision and the link to original batch cost.
8. Define stocktake lock representation and recovery after session cancellation/failure; no scoped stock movement may pass while its freeze is active.
9. Define the central/branch projection mapping for each shared business identifier and the exact synchronization contract version.
10. Define EF Core delete behaviors explicitly. Financial, stock, audit, return, payment, synchronization and approval history must not cascade-delete.

## 14. Next design sequence

1. Review and approve/correct TD-13 and TD-14 together.
2. Resolve the relationship risks listed above using the already approved business rules.
3. Produce detailed table specifications in dependency order: Organization/Access; Catalog; Procurement; Inventory; Pricing; POS/Sales/Returns; Payments; Online/CRM/Fulfillment; Operations/Audit/Reporting; Synchronization.
4. For every table, specify columns, nullability, PK/FK, unique/check constraints, indexes, concurrency token, audit fields and deletion behavior.
5. Review all local transaction boundaries and cross-site message contracts.
6. Only after logical and physical schema approval, create EF Core entities/configurations and then migrations.

**No SQL table, EF Core entity, DDL or migration is created or authorized by this relationship model.**
