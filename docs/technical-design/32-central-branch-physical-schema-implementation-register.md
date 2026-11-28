# TD-32 — Central and Branch Physical Schema Implementation Register

**Version:** 0.1  
**Status:** In progress — ownership baseline reconciled; detailed physical mappings are being completed in implementation order.  
**Date:** 2026-11-28  
**Depends on:** TD-12 through TD-31.  
**Purpose:** Establish the authoritative physical table boundary for the Central database and each Branch database before DbContext configuration and migrations are generated.

> This register is not a migration and does not authorize a partial production database. The detailed table definitions in TD-12 and TD-15 through TD-21 remain the source specifications where present. Missing detailed modules must be completed before their tables are added to the corresponding DbContext/migration.

## 1. Physical database boundaries

- Central database: `Supermarket_Central`.
- One independent database per Branch: `Supermarket_Branch_{BranchCode}`.
- No cross-database foreign keys, joins, distributed transactions, or shared DbContext.
- Central owns centrally governed master data, approvals, online workflows, and synchronized Branch facts/projections.
- Each Branch owns its physical stock, POS sales, shifts, cash drawer, local receiving/fulfillment execution facts, and durable local messaging state.
- A projection is read-only locally and cannot become a second authority.
- Every PK is sequential application-generated `Guid` / SQL `uniqueidentifier`; UTC instants are `datetime2(3)`; JOD amounts and quantities use `decimal(18,3)`; mutable concurrency-controlled rows use `rowversion`.
- Delete behavior defaults to Restrict/No Action. Historical financial, stock, audit and synchronization facts are not cascade-deleted.

## 2. Authoritative corrections applied during reconciliation

1. **Identity exclusivity:** TD-29 supersedes the earlier TD-15/TD-13 physical-enforcement suggestion. `auth.Users.AccountType` plus Application Layer validation governs exactly one Employee or Customer profile. No XOR CHECK/trigger/shared registry is introduced.
2. **Supplier payments:** TD-31 supersedes the old one-payment/one-invoice assumption in TD-17. Central `SupplierPayments` is a payment header with `SupplierPaymentAllocations`; one payment may settle multiple invoices. Allocation totals and invoice outstanding caps are checked transactionally.
3. **Rounding:** TD-31 fixes the final invoice rounding rule to .NET `MidpointRounding.AwayFromZero`; intermediate calculations retain precision.
4. **Offline identity:** Branch stores only a protected local verifier/session representation, never Central password hashes, reusable Central tokens or security stamps. Cryptographic design/key lifecycle remains a security gate.
5. **Synchronization:** TD-12 is the sole physical authority for `sync.OutboxMessages`, `sync.InboxMessages`, `sync.ProcessedOperations`, `sync.BranchCommands`, and `sync.SyncCheckpoints`. No duplicate generic command-delivery table is allowed.
6. **No database-level cross-site references:** Any identifier that points to a record in the other database is a correlation GUID, not an FK.

## 3. Central database table register

The following table groups are created only in Central unless explicitly marked as a synchronized Central projection.

### 3.1 Organization and Identity
- `auth.Users`, `auth.UserRoles`
- `org.Branches`, `org.Employees`, `org.EmployeeBranchAssignments`
- `org.Roles`, `org.Permissions`, `org.RolePermissions`, `org.EmployeePermissionGrants`
- `org.RegisteredDevices`, `org.EmployeeSessions`, `org.RuntimePolicies`
- Central publication/administrative metadata for Branch access snapshots only where required; Branch snapshot employee/permission rows remain Branch-owned.

Identity decisions: Username and normalized username are required and unique; login is username + password only. Email/phone are optional contact methods, not login names. At least one contact method must be confirmed to activate/login. Five failed attempts cause a 15-minute lockout. Idle timeout is 30 minutes. Disabled accounts revoke active sessions. Password minimum is 12 characters and must be changed after creation/reset. MFA is not enabled in this release. Login attempts are recorded in `auth.LoginAttempts` (UserId nullable for unknown accounts; never store secrets). The exact Identity mapping is completed in the Identity physical sub-specification before migration. The approved login-attempt audit table is `auth.LoginAttempts`; its nullable UserId supports unknown usernames.

### 3.2 Product catalogue
- `catalog.ProductCategories`, `catalog.Brands`, `catalog.Countries`, `catalog.Products`
- `catalog.ProductBranchAvailability`, `catalog.ProductBarcodes`, `catalog.ProductUnits`, `catalog.ProductUnitConversions`
- `catalog.CompositeProductComponents`, `catalog.ProductAttributeDefinitions`, `catalog.ProductAttributes`, `catalog.ProductMedia`
- Central decision authority for `catalog.ProductRequests`, `catalog.ProductRequestLines`, `catalog.ProductRequestDecisions`; Branch retains its originating request copy.

Product decisions: globally unique immutable SKU; canonical active barcode cannot be assigned to multiple products; one inventory base unit per product and multiple sellable units are allowed; UnitKind values are Piece, Weight, Volume, Length and Package; typed attributes are Text, Number, Boolean and Date; media binaries live in protected object storage with SQL metadata; composite-product cycles are prohibited; category changes remain auditable.

### 3.3 Supplier and procurement authority
- `procurement.Suppliers`, `procurement.SupplierContacts`, `procurement.SupplierProductTerms`, `procurement.SupplierReturnTerms`, `procurement.SupplierProductPrices`, `procurement.SupplierProductIdentifiers`
- `procurement.PurchaseRequests`, `procurement.PurchaseRequestLines`, `procurement.PurchaseRequestDecisions`, `procurement.PurchaseRequestDecisionLines`
- `procurement.PurchaseOrders`, `procurement.PurchaseOrderLines`
- Central synchronized projections of `procurement.PurchaseReceipts` and `procurement.PurchaseReceiptLines`; Central-owned `PurchaseReceiptDecisions` and `PurchaseReceiptDecisionLines`
- Central approval copies of `procurement.SupplierInvoices` and `procurement.SupplierInvoiceLines`; `SupplierInvoiceMatchLines`, `SupplierInvoiceDiscrepancies`, `SupplierInvoiceDueDates`
- `procurement.SupplierPayments`, `procurement.SupplierPaymentAllocations`
- Central workflow copies of `procurement.SupplierReturns`, `procurement.SupplierReturnLines`, `procurement.SupplierReturnSettlements`, and synchronized `SupplierReturnEvents`.

Supplier decisions: centrally assigned immutable SupplierCode; one active primary contact per supplier; multiple suppliers per product; supplier product identifiers are unique within a supplier; supplier prices are effective-dated and do not rewrite PO snapshots; supplier return terms are versioned; statuses Pending/Approved/Suspended; no new PO for a non-Approved supplier; referenced suppliers are not deleted.

Purchasing/receiving decisions: a request may contain multiple lines; approval does not automatically create a PO; each PO belongs to one supplier; a request may be fulfilled by multiple POs; approved PO amendments are explicit audited revisions; partial receipts are allowed; exceptional receiving requires Central authorization before stock entry; excess quantities remain unavailable until approved; accepted stock and its batch/movement effects are atomic in the Branch DB; actual batch cost is retained independently from the quoted/ordered price. Supplier invoice matching is three-way (PO, receipt, invoice); one invoice line may match multiple receipt lines. Supplier payments are Central-only and use payment header plus allocation rows.

### 3.4 Inventory governance
- Central stocktake workflow: `inventory.Stocktakes`, `StocktakeScopeLines`, `StocktakeFreezeAcknowledgements`, `StocktakeAssignments`, `StocktakeLines`, `StocktakeRecounts`, `StocktakeVarianceCases`, `StocktakeVarianceDecisions`, `StocktakeSessionEvents`.
- Central transfer authority: `inventory.StockTransfers`, `StockTransferLines`, `StockTransferEvents`; synchronized `StockTransferBranchEvents`.
- Central disposal authority: `inventory.InventoryDisposalRequests`, `InventoryDisposalRequestLines`, `InventoryDisposalDecisions`, `InventoryDisposalDecisionLines`; synchronized `InventoryDisposals`, `InventoryDisposalLines`.
- `inventory.ReorderPolicies`, `inventory.ExpiryAlertPolicies`, `inventory.StockIncreaseRequests`, `inventory.StockIncreaseRequestDecisions`.
- Central synchronized projections of Branch warehouses, batches, balances, movements, reservations and quarantines where reporting/control requires them. They are not operational stock authority.

### 3.5 Pricing and promotions
- `pricing.PriceVersions`, `pricing.ProductPrices`, `pricing.PromotionVersions`, `pricing.Promotions`, `pricing.PromotionConditions`, `pricing.PromotionRewards`, `pricing.Coupons`, `pricing.PromotionUsageQuotas`, `pricing.CouponUsageQuotas`
- Central authority for `pricing.CouponRedemptions` and `pricing.PromotionRedemptions`, with synchronized Branch use events as specified by Offline rules.
- Branch-applied version/hash records remain Branch-owned.

Promotion decisions: no stacking by default; explicit opt-in stacking only; priority descending then PromotionId ascending; allowlisted versioned condition/reward operators only; no executable SQL/C#/script stored in data; snapshots retained on invoices/orders.

### 3.6 Payments and online commerce
- Central provider authority: `payments.PaymentProviders`, `PaymentProviderConfigurations`, `PaymentMethods`, `PaymentProviderEvents`, `PaymentReconciliations`, `PaymentReconciliationLines`.
- `payments.PaymentTransactions` and `PaymentAttempts` for online transactions; Central-owned online refund/exception workflows and synchronized Branch refund facts as specified by TD-21.
- `online.Carts`, `CartLines`, `Orders`, `OrderLines`, `OrderStatusHistory`, `OrderReservations`, `OrderDeliveryDetails`, `DeliveryCities`, `DeliveryAreas`, `DeliveryFeeRules`, `FreeDeliveryRules`, `StorePolicies`, `OrderCancellationRequests`, `OrderCancellationDecisions`, `OrderCancellationDecisionLines`, `OrderNotifications`, `NotificationDeliveryAttempts`.
- `fulfillment.FulfillmentTasks`, `FulfillmentExceptionDecisions`, `FulfillmentExceptionDecisionLines`, `DeliveryShiftCoordinators`, `DeliveryAssignments`, `PickupRetentionPolicies`, `PickupNonCollectionCases`, `PickupNonCollectionDecisions`, `DeliveryDisputes`, `DeliveryDisputeDecisions`.
- Central projections of Branch payment, sales, refund, picking, delivery and pickup execution evidence; Branch remains the source of physical execution.

### 3.7 Customer, loyalty and complaints
- `crm.Customers`, `CustomerPhoneVerifications`, `CustomerDuplicateResolutions`, `LoyaltyProgramRules`, `LoyaltyAccounts`, `LoyaltyPointTransactions`, `LoyaltyPointLots`, `LoyaltyPointLotAllocations`, `CustomerComplaints`, `ComplaintActions`, `ComplaintAssignments`.
- Central is authoritative for customer profile, loyalty ledger/balance, complaints and duplicate resolution. Branch may hold only restricted customer projections and permitted Offline earning events. Loyalty redemption remains Online-only.

### 3.8 Operations, maintenance, alerts, audit and reporting
- `operations.ExpenseCategories`, `ExpenseApprovals`, `ExpenseRequestDocuments`, `PayrollTrackingRecords` (placeholder only), `CashDeposits`, `CashDepositLines`; Central review projections for `ExpenseRequests` and `ExpenseExecutions`.
- `maintenance.MaintainableAssets`, `MaintenanceAssignments`, `MaintenanceWorkLogs`, `MaintenanceDocuments`, `MaintenanceRequestEvents`; Central workflow copies for `MaintenanceRequests`.
- `alerts.AlertDefinitions`, `AlertEvents`, `AlertRecipients`, `AlertDeliveries`, `AlertAcknowledgements`, `AlertActions`.
- `audit.AuditEntries`, `AuditChangeDetails`, `SecurityEvents`, `SecurityEventReviews`.
- Optional `reporting.ReportRunRecords`; reporting read models are projections, not alternate authorities.

### 3.9 Central synchronization
- `sync.OutboxMessages`, `sync.InboxMessages`, `sync.ProcessedOperations`, `sync.BranchCommands`, `sync.SyncCheckpoints`.
- TD-12 remains the exact physical definition for these five tables.

## 4. Branch database table register

### 4.1 Local access and approved snapshots
- `org.BranchAccessSnapshots`, `org.BranchAccessSnapshotEmployees`, `org.BranchAccessSnapshotPermissions`, `org.BranchAccessSnapshotDevices` (new projection table), `org.BranchEmployeeSessions` (new local session-state table), `org.BranchRuntimePolicies`.
- Minimum approved device/session/offline verifier state only; never copy Central password hashes, security stamps or reusable tokens.

### 4.2 Read-only Central projections
- Product catalogue: categories, brands, countries, products, branch availability, barcodes, units, conversions, composite components, attribute definitions/values and media metadata.
- Supplier/product references and approved PO/receiving task projection where required.
- Approved price/promotion/coupon snapshots and version metadata.
- Restricted customer/loyalty projections only for approved workflows; no local loyalty redemption.
- Asset/reference and policy projections needed for Branch execution.
- Each projection stores Central source ID, source version, synchronization metadata and local application status; it is never locally editable as master data.

### 4.3 Branch operational authority
- Procurement: originating `PurchaseRequests` and lines; authoritative `PurchaseReceipts` and lines; originating supplier invoice registration and lines; physical supplier return execution events and request evidence.
- Inventory: `inventory.Warehouses`, `WarehouseLocations`, `InventoryBatches`, `InventoryBalances`, `InventoryMovements`, `InventoryMovementLines`, `StockReservations`, `StockReservationAllocations`, `StocktakeLocks`, `InventoryQuarantines`, and physical disposal execution records/lines.
- POS: `sales.CashierShifts`, `CashDrawerReconciliations`, `SuspendedSales`, `SuspendedSaleLines`, `CashDrawerTransactions`, `SalesInvoices`, `SalesInvoiceLines`, `SalesInvoiceDiscounts`, `SalesInvoiceBatchIssues`, `CompositeSaleComponentIssues`, `SalesReturns`, `SalesReturnLines`, `SalesReturnBatchAllocations`, `SalesReturnInspections`, `SalesReturnDispositions`, `ExchangeLinks`, `InvoiceSequences`, `SalePaymentAllocations`, `InvoicePrintRecords`.
- Payments/refunds: local POS `PaymentTransactions`, `PaymentAttempts` (cashier-recorded independent terminal outcomes only), local cash refunds and refund execution records, and originating refund-method exception requests. No Branch provider authority or provider callback processing.
- Fulfillment: `PickingSessions`, `PickingLines`, `PickingLineScans`, `DeliveryAttempts`, `DeliveryProofs`, `FailedDeliveryReturns`, `FailedDeliveryReturnLines`, `DeliveryReturnInspections`, `PickupHandovers`; assigned Central tasks/exceptions/assignments are read-only projections.
- Operations: originating `ExpenseRequests`, execution evidence `ExpenseExecutions`, originating `MaintenanceRequests`, and branch confirmation/evidence for maintenance work.
- Audit: local `audit.AuditEntries`, `AuditChangeDetails`, `SecurityEvents`.
- Synchronization: `sync.OutboxMessages`, `sync.InboxMessages`, `sync.ProcessedOperations`, `sync.SyncCheckpoints`; no `sync.BranchCommands`.

## 5. Physical schema supplements completed under delegated authority

- TD-32A: Central Identity and Organization physical schema, including `auth.LoginAttempts` and unified Identity mapping.
- TD-32B: Central Product Catalog physical decisions and indexes.
- TD-32C: Central Suppliers, Purchasing, Receiving, invoice matching, supplier returns and multi-invoice payment allocations.
- TD-32D: Branch access snapshots, local session state and Offline policy projection. Adds `org.BranchAccessSnapshotDevices` and `org.BranchEmployeeSessions` to the Branch physical inventory.
- TD-32E: Branch inventory balances, batches, ledger, exact reservation allocations, stocktake freeze, quarantine and disposal.
- TD-32F: Central pricing/promotions, deterministic rules, atomic Branch publication and offline quota allocation. Adds Central `pricing.PromotionUsageQuotas` and `pricing.CouponUsageQuotas` with Branch-local projections.

These supplements refine or supersede earlier logical wording where explicitly stated. They must be reconciled back into TD-13/TD-14 and module specifications before migrations.

## 6. EF Core implementation boundary

- One `CentralDbContext` in `Central.Infrastructure`; one `BranchDbContext` in `Branch.Infrastructure`.
- Entity configurations are grouped by module and explicitly map schema, table, key, precision, max lengths, constraints, filtered indexes and rowversion.
- Migrations are isolated: Central migrations only modify Central DB; Branch migrations only modify the selected Branch DB.
- No Domain/Application project references EF Core.
- Do not generate a partial initial migration until every table in the applicable first-release schema has an approved physical definition.
- Use SQL Server integration tests for constraints, filtered unique indexes, rowversion, idempotency, stock concurrency, refund caps, and atomic sequences.

## 7. Implementation sequence

1. Finalize Central detailed physical definitions in this order: Identity/Organization; Product Catalog; Suppliers/Procurement/Receiving/Invoices; Supplier Payments; Pricing/Promotions; Online Orders/Payments; Customers/Loyalty; approvals/operations/maintenance/alerts/audit/reporting; Central synchronization.
2. Finalize Branch detailed physical definitions: Access/Offline; projections; Inventory/Batches/Reservations; POS/Sales/Returns; Shifts/Cash Drawer; Receiving/Fulfillment; Branch synchronization.
3. Reconcile every FK, cross-database correlation GUID, index, status code, snapshot, audit and transaction boundary against TD-14, TD-24, TD-26 and TD-31.
4. Approve the completed Central and Branch schema packages.
5. Add entities, configurations and the two DbContexts; create the initial migrations and validate them against SQL Server 2025.
6. Implement the first vertical slice only after schema migration succeeds.

## 8. Known completion gates

- TD-22 (`22-online-crm-fulfillment-detailed-schema.md`) and TD-23 (`23-operations-maintenance-alerts-audit-reporting-schema.md`) both exist and are detailed; TD-24 confirms 205 domain tables have detailed specifications. They must be reconciled against later decisions, not copied mechanically.
- Exact Identity EF Core table mapping and required `auth.LoginAttempts` physical definition must be finalized before Identity migration.
- Supplier payment allocations must be represented explicitly.
- Any Central projection of Branch facts must retain source BranchId and source record GUID and must not be treated as a second editable authority.
- SQL Server integration/build validation is still required in an environment with the .NET 10 SDK and SQL Server 2025.

## 9. Approval record

This register applies the delegated design authority granted by the project owner on 2026-11-28. It records design decisions for implementation; it does not authorize merging a pull request or deploying a database.
