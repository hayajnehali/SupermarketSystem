# TD-13 — Complete Database Table Inventory and Ownership Blueprint

**Version:** 0.1  
**Status:** Proposed for owner review  
**Date:** 2026-11-07  
**Depends on:** Approved BRS decisions, TD-01, TD-03, TD-04, TD-05, TD-09, TD-11 and approved physical messaging design TD-12.  
**Purpose:** Establish a complete first-pass inventory of the relational data areas and candidate tables before detailed entity/column design.  
**Important:** This is a logical inventory, not executable DDL, EF Core entities, or migrations. Names and boundaries are proposed for review and may be refined during detailed modeling. No business rule is changed here.

## 1. Database topology and ownership notation

The system has one Central database and one independently operated database per Branch. There is no shared cross-site database and no cross-database foreign key.

| Mark | Meaning |
|---|---|
| **C** | Authoritative record is stored in the Central database. |
| **B** | Authoritative operational record is stored in each applicable Branch database. |
| **C+B projection** | Central and Branch databases each hold their own local copy/projection for the stated purpose. This is not a shared table or shared transaction. |
| **C+B distinct** | Separate records may be created at both sides, with ownership and synchronization direction explicitly defined. |

A projection is a locally stored representation of centrally owned data or synchronized branch facts. It must not become a second authority. Branch copies of central master data are read-only and versioned.

## 2. System-wide database rules

1. Every table primary key is `Guid` in C# and `uniqueidentifier` in SQL Server. No integer identity primary keys.
2. Foreign keys use the same `Guid` type as the referenced primary key.
3. Integer and `bigint` columns remain valid for quantities, counters, sequence numbers, and ordering; they are not primary keys.
4. A foreign key may exist only when both rows live in the same database boundary. Cross-database relationships use stable GUID references and application contracts, not SQL foreign keys.
5. Each module owns its tables. Another module must not query or update those tables directly; use application contracts or integration events.
6. Branch physical stock, POS invoices, and local cash-shift activity are authoritative in the relevant Branch database. Central stores synchronized history/projections.
7. Centrally governed master data (branches, employees, roles, product catalog, suppliers, prices, promotions, policies and customer/loyalty master) is authoritative in Central. Branches store only the approved local projections required for permitted operation.
8. Financial and historical transaction values are snapshots. Later product, price, promotion, employee, or supplier edits must not rewrite completed records.
9. Soft deletion/deactivation is used for master data where history depends on it. Financial, stock, audit, and synchronization history is not physically deleted through ordinary workflows.
10. Status history may be represented by append-only history/event tables where business traceability requires it; do not create duplicate history tables where the authoritative audit/event record already provides the required history.

## 3. Candidate table inventory by module

The following is the complete first-pass inventory derived from the BRS units 01–12 and the approved TD-03 module map. It deliberately separates authoritative records from branch projections. A table marked C+B projection must be modeled separately in each database and synchronized through the approved boundary.

### 3.1 Organization and access

| Candidate table | Placement | Purpose / notes |
|---|---|---|
| `org.Branches` | C | Branch master, branch code, activation/suspension and configuration identity. |
| `org.Employees` | C | Employee profile and employment status; identity credentials remain in the identity subsystem. |
| `org.EmployeeBranchAssignments` | C | Effective-dated employee-to-branch assignments and history. |
| `org.Roles` | C | Centrally managed roles. |
| `org.Permissions` | C | Permission catalogue. |
| `org.RolePermissions` | C | Role-to-permission mapping. |
| `org.EmployeePermissionGrants` | C | Additional employee grants/revocations independent of role. |
| `org.RegisteredDevices` | C; B local validation projection | Device registration, status, ownership and revocation metadata. |
| `org.EmployeeSessions` | C; B local offline-session state | Session/revocation records needed for centrally managed sessions and bounded local validation. |
| `org.BranchAccessSnapshots` | B | Approved branch-local employee/role/permission snapshot required for Offline authorization; not an authority. |
| `org.BranchAccessSnapshotEmployees` | B | Effective employee authorization entries within the active local snapshot. |
| `org.BranchAccessSnapshotPermissions` | B | Permission rows attached to a snapshot employee entry; local snapshot data only.
| `org.BranchRuntimePolicies` | B projection of C | Last centrally approved Offline/runtime policy and version. |

**Identity implementation note:** ASP.NET Core Identity persistence tables are framework-owned implementation tables. Their exact schema and relationship to `org.Employees` will be specified in the identity design; do not create a second custom password/credential store. Local Offline authentication material must follow TD-06 security constraints.

### 3.2 Product catalog

| Candidate table | Placement | Purpose / notes |
|---|---|---|
| `catalog.ProductCategories` | C; B read-only projection | Hierarchical categories/subcategories. |
| `catalog.Brands` | C; B read-only projection | Centrally managed product brands displayed in the catalogue. |
| `catalog.Countries` | C; B read-only projection | Country-of-origin reference values used by product details. |
| `catalog.ProductRequests` | B request origin; C approval authority | Branch requests for a new product or product change; central approval precedes activation. |
| `catalog.ProductRequestLines` | Same as parent | Requested product attributes, units, barcodes and supporting details. |
| `catalog.ProductRequestDecisions` | C | Central approve/reject/return-for-correction decisions with actor, reason and time. |
| `catalog.Products` | C; B read-only projection | Product/SKU master and centrally managed attributes. Distinct SKU for each distinct size/type. |
| `catalog.ProductBarcodes` | C; B read-only projection | One or more barcodes per product; uniqueness enforced at the correct scope. |
| `catalog.ProductUnits` | C; B read-only projection | Selling/base units and open-pack capability. |
| `catalog.ProductUnitConversions` | C; B read-only projection | Approved conversion factors and precision. |
| `catalog.CompositeProductComponents` | C; B read-only projection | Component definitions for composite products. |
| `catalog.ProductAttributes` | C; B read-only projection | Optional structured attribute definitions/values where required by catalog search/display. |
| `catalog.ProductMedia` | C; B read-only projection or media reference | Product image/media metadata; binary files should use managed file/object storage, not database blobs by default. |

### 3.3 Suppliers and procurement

| Candidate table | Placement | Purpose / notes |
|---|---|---|
| `procurement.Suppliers` | C | Supplier master, activation and terms. |
| `procurement.SupplierContacts` | C | Supplier contact details. |
| `procurement.SupplierProductTerms` | C | Supplier/product-specific supply and return terms where recorded. |
| `procurement.SupplierProductPrices` | C | Supplier-specific purchase price history/effective values; actual received batch cost remains on the inventory batch. |
| `procurement.PurchaseRequests` | C authoritative workflow; B request origin/projection | Branch needs request and central review/decision. |
| `procurement.PurchaseRequestLines` | Same as parent | Requested products and quantities. |
| `procurement.PurchaseRequestDecisions` | C | Central approve/reject/adjust/return-for-correction decisions with actor, reason and time. |
| `procurement.PurchaseOrders` | C | Centrally created/approved supplier purchase orders. |
| `procurement.PurchaseOrderLines` | C | Ordered products, quantities, prices and terms. |
| `procurement.PurchaseReceipts` | B execution record; C synchronized projection | Full/partial receiving and exceptional receiving evidence. |
| `procurement.PurchaseReceiptLines` | Same as parent | Actual, ordered, accepted and excess quantities, with batch/cost/expiry references. |
| `procurement.PurchaseReceiptDecisions` | C | Central approval/rejection of excess quantities and exceptional receiving differences before stock availability. |
| `procurement.SupplierInvoices` | B registration; C approval authority | Branch records supplier invoice; Central performs three-way match and approval. |
| `procurement.SupplierInvoiceMatchLines` | C | Match invoice lines to approved purchase-order lines and actual receipt lines. |
| `procurement.SupplierInvoiceDiscrepancies` | C | Quantity/price/terms mismatch requiring central review before invoice approval. |
| `procurement.SupplierInvoiceDueDates` | C | Contractual due date/installment schedule used for central reminders. |
| `procurement.SupplierPayments` | C | Central-approved and centrally executed supplier payment records; not a general ledger or accounts-payable subledger. |
| `procurement.SupplierReturns` | C approval workflow; B execution record/projection | Supplier return request, approval and outcome. |
| `procurement.SupplierReturnLines` | Same as parent | Product, batch, quantity, reason and compensation linkage. |

### 3.4 Inventory and stock

| Candidate table | Placement | Purpose / notes |
|---|---|---|
| `inventory.Warehouses` | B; C synchronized reference/projection | Branch warehouses. |
| `inventory.WarehouseLocations` | B; C projection if needed | Sales floor, backroom, fridges and other storage locations. |
| `inventory.InventoryBatches` | B authoritative; C projection | Batch-level remaining quantity, cost, source and expiry when applicable. |
| `inventory.InventoryBalances` | B authoritative projection | Current quantity by product/unit/location/batch and stock state; derived from posted movements. |
| `inventory.InventoryMovements` | B authoritative; C immutable synchronized history | Receipts, issues, sales, returns, transfers, quarantine, adjustments and other stock events. |
| `inventory.InventoryMovementLines` | Same as parent | Product/unit/batch/quantity/cost detail. |
| `inventory.StockReservations` | B authoritative for physical reservation; C coordination record/projection | Website order reservations and other explicitly approved reserved quantities. The branch's atomic stock state remains final for acceptance and POS exclusion. |
| `inventory.StockReservationAllocations` | B | Optional exact batch allocations for a reservation when batch-level reservation is required; one reservation may span multiple batches. |
| `inventory.Stocktakes` | C | Central-created and controlled stocktake session; central employees perform the physical count at the branch. |
| `inventory.StocktakeScopeLines` | C | Explicit product/area scope for full-branch or partial stocktake. |
| `inventory.StocktakeFreezeAcknowledgements` | C record of B acknowledgement | Confirms the branch has frozen stock-changing operations for the session scope before counting. |
| `inventory.StocktakeLocks` | B | Durable local enforcement of active stocktake freeze by product/location scope; released only by an authorized central command after session closure/cancellation. |
| `inventory.StocktakeAssignments` | C | Assigned counters and scoped responsibility. |
| `inventory.StocktakeLines` | C | Blind count entries by central counters; expected stock is hidden during entry. |
| `inventory.StocktakeRecounts` | C | Second count and any further controlled recount attempts. |
| `inventory.StocktakeVarianceCases` | C | Individually tracked variance case per product/scope, reason and investigation. |
| `inventory.StocktakeVarianceDecisions` | C | Central investigation result and approval/rejection of adjustment. |
| `inventory.StocktakeSessionEvents` | C | Start, cancel, close and freeze-release history; cancellation requires reason. |
| `inventory.StockTransfers` | C | Branch-originated transfer request; central review, approval and execution control. |
| `inventory.StockTransferLines` | C | Products, source/destination branches, quantities, batch and cost lineage. |
| `inventory.StockTransferBranchEvents` | B source/destination event facts; C synchronized | Branch-side execution acknowledgements and stock-ledger posting references generated from central commands. |
| `inventory.StockTransferEvents` | C | Central transfer lifecycle and command history; branch physical stock effects remain in each branch ledger. |
| `inventory.InventoryQuarantines` | B authoritative; C projection | Quarantined/damaged/expired stock, reason, batch, quantity, actor and timestamp. |
| `inventory.InventoryDisposalRequests` | B request origin; C approval authority | Branch submits isolated damaged/expired stock for central disposal approval. |
| `inventory.InventoryDisposalRequestLines` | Same as parent | Product, batch, quantity, reason and quarantine linkage. |
| `inventory.InventoryDisposalDecisions` | C | Central approval/rejection before physical disposal. |
| `inventory.InventoryDisposals` | B execution fact; C synchronized record | Actual disposal only after approval, including executor, date/time and evidence. |
| `inventory.InventoryDisposalLines` | Same as parent | Actual disposed quantities and batch/cost references. |
| `inventory.ReorderPolicies` | C policy; B approved local projection | Centrally approved branch/product reorder thresholds. |
| `inventory.StockIncreaseRequests` | B request origin; C review/decision authority | Branch may submit a stock-increase request; Central also monitors thresholds proactively and does not depend on branch requests. |
| `inventory.StockIncreaseRequestDecisions` | C | Central approval/rejection history for branch-submitted stock-increase requests. |

**Inventory modeling constraints:** FIFO applies to non-expiry-tracked products and FEFO to expiry-tracked products according to product policy. Batch issue details must be retained against sales and returns. A balance is a controlled projection, not a replacement for the stock movement ledger. Do not use weighted-average costing. Central employees conduct stock counts at branches; Central defines the full/partial scope, freezes the relevant stock-changing operations, controls recounts and investigates each variance. Branch stock effects from approved adjustments, transfers and disposal are applied only through authorized commands and recorded in the branch stock ledger.

### 3.5 Pricing, promotions and coupons

| Candidate table | Placement | Purpose / notes |
|---|---|---|
| `pricing.PriceVersions` | C | Versioned price publication/effective periods. |
| `pricing.ProductPrices` | C; B approved price projection | Centrally approved base prices with effective dates. |
| `pricing.Promotions` | C; B approved promotion projection | Promotion definitions, validity and status. |
| `pricing.PromotionConditions` | C; B projection | Eligibility/condition rules. |
| `pricing.PromotionRewards` | C; B projection | Discount/reward effects and calculation inputs. |
| `pricing.Coupons` | C; B projection only if required for local validation | Centrally managed coupon codes/rules. |
| `pricing.CouponRedemptions` | C authoritative; B durable local event only when Offline redemption is permitted | Redemption use and idempotency linkage. |

Branch pricing data is an approved versioned snapshot. Expired promotions stop applying; the last approved base price remains the fallback under the approved Offline rule. Historical invoice snapshots are immutable.

### 3.6 POS, sales, shifts and cash drawers

| Candidate table | Placement | Purpose / notes |
|---|---|---|
| `sales.CashierShifts` | B authoritative; C synchronized history | Opening/closing, cashier, device, branch and shift state. |
| `sales.CashDrawerTransactions` | B authoritative; C synchronized history | Opening float, cash in/out and recorded drawer movements. |
| `sales.SalesInvoices` | B authoritative; C immutable synchronized history | Completed/cancelled invoice header and historical totals; includes `SequenceYear` and branch/year invoice number. |
| `sales.SalesInvoiceLines` | B authoritative; C immutable synchronized history | Product/unit/quantity/price/discount snapshots. |
| `sales.SalesInvoiceBatchIssues` | B authoritative; C synchronized history | Exact inventory batches and costs issued to each invoice line. |
| `sales.CompositeSaleComponentIssues` | B authoritative; C synchronized history | Component quantities and batch costs consumed by a composite-product sale, linked to the invoice line. |
| `sales.SalesReturns` | B authoritative; C synchronized history | Return header linked to original invoice; no-invoice exceptions require central referral. |
| `sales.SalesReturnLines` | Same as parent | Returned product, unit, quantity/weight, original sale price/discount and original batch-cost references. |
| `sales.SalesReturnInspections` | B execution fact; C synchronized | Inspection outcome per returned item: sellable, quarantine/review, or damaged/expired. |
| `sales.SalesReturnDispositions` | B execution fact; C synchronized | Authorized return-to-stock or quarantine/disposal disposition; disposal remains subject to central approval. |
| `sales.ExchangeLinks` | B authoritative; C synchronized history | Links a return to its replacement sale without merging their financial/stock records. |
| `sales.InvoiceSequences` | B authoritative | Branch/year sequential number allocator; sequence value is not a primary key. |
| `sales.SalePaymentAllocations` | B authoritative; C synchronized financial history | Split tender allocation against an invoice; only confirmed full payment completes a sale. |
| `sales.InvoicePrintRecords` | B | Optional print/reprint audit; reprint never creates another invoice or stock movement. |

### 3.7 Payments and refunds

| Candidate table | Placement | Purpose / notes |
|---|---|---|
| `payments.PaymentMethods` | C; B approved configuration projection | Centrally enabled cash/electronic tender types. |
| `payments.PaymentTransactions` | B for POS tender; C for online payment | Financial transaction identity and final result by channel. |
| `payments.PaymentAttempts` | Same authority as parent transaction | Provider/terminal attempts, pending/failed/success outcomes. |
| `payments.PaymentProviderEvents` | C | Durable provider callbacks/webhook events and deduplication evidence. |
| `payments.Refunds` | C for online and centrally approved cases; B execution record for permitted local cash refund | Refund decision and lifecycle, linked to original payment/sale. |
| `payments.RefundLines` | Same as parent | Returned invoice lines/quantities/amounts where line allocation is required. |
| `payments.RefundAttempts` | Same as parent | Provider refund attempts and outcomes; repeated notifications cannot duplicate the financial effect. |
| `payments.PaymentReconciliations` | C | Reconciliation batches/results for payment provider and recorded transactions. |

Do not introduce accounts receivable, customer debt, credit balances or a general ledger. Payment, refund and return states remain separately traceable.

### 3.8 Online store and customer orders

| Candidate table | Placement | Purpose / notes |
|---|---|---|
| `online.Carts` | C | Authenticated customer's active cart. |
| `online.CartLines` | C | Cart products/units/quantities before confirmation. |
| `online.Orders` | C authoritative; assigned B operational projection | One order belongs to one selected branch; no split fulfillment. |
| `online.OrderLines` | C authoritative; assigned B projection | Immutable confirmed product/price/discount/quantity snapshots. |
| `online.OrderStatusHistory` | C | Order lifecycle transitions; payment/refund state is not collapsed into order status. |
| `online.OrderReservations` | C coordination; linked to B authoritative `inventory.StockReservations` | Central 15-minute reservation/payment lifecycle and branch acknowledgement references. |
| `online.OrderDeliveryDetails` | C; assigned B minimal projection | Per-order selected city/area and free-text address details. No reusable saved-address book is introduced. |
| `online.DeliveryCities` | C | Centrally managed city reference data. |
| `online.DeliveryAreas` | C | Areas/regions belonging to a city. |
| `online.DeliveryFeeRules` | C | Central delivery fee by area and approved free-delivery conditions. |
| `online.StorePolicies` | C | Central website policies such as minimum order amount and effective configuration version. |
| `online.OrderCancellationRequests` | C | Customer cancellation request before preparation, routed for central approval. |
| `online.OrderCancellationDecisions` | C | Central approve/reject decision with actor, time and reason. |

| `online.OrderNotifications` | C | Website/email customer notification records. |
| `online.NotificationDeliveryAttempts` | C | Delivery attempt/result history; notification failure does not change order/payment state. |

The 15-minute window, late-payment handling, central cancellation approval before preparation, no customer content edits after confirmation, area-based delivery fees, minimum order amount and central decision on fulfillment unavailability remain governed by the approved BRS and TD-05/TD-09 contracts. Customer selects a city and area and enters free-text address details per order; no saved address book or delivery-time selection is introduced.

### 3.9 Fulfillment, picking, pickup and delivery

| Candidate table | Placement | Purpose / notes |
|---|---|---|
| `fulfillment.FulfillmentTasks` | C authoritative assignment; B assigned-work projection | Work item for the selected branch/order. |
| `fulfillment.FulfillmentExceptions` | B execution fact; C decision authority | Picking shortages/unavailability and related evidence reported to Central. |
| `fulfillment.FulfillmentExceptionDecisions` | C | Central decision for order exception/cancellation; branch cannot independently cancel the confirmed website order. |
| `fulfillment.PickingSessions` | B | Picking execution session and responsible employee. |
| `fulfillment.PickingLines` | B | Picked quantities and barcode verification results. |
| `fulfillment.DeliveryShiftCoordinators` | C | Central appointment of coordinator by shift. |
| `fulfillment.DeliveryAssignments` | C assignment authority; B assigned-task projection | Assigned delivery employee, coordinator and timestamps. |
| `fulfillment.DeliveryAttempts` | B execution fact; C synchronized record | Exactly one permitted customer delivery attempt; preserve outcome. |
| `fulfillment.DeliveryProofs` | B execution fact; C synchronized reference | Manual confirmation/evidence references; no OTP/e-signature requirement is introduced. |
| `fulfillment.PickupHandovers` | B execution fact; C synchronized record | Pickup handover and completion. |
| `fulfillment.DeliveryDisputes` | C | Customer dispute record; no time limit is introduced. |
| `fulfillment.DeliveryDisputeDecisions` | C | Central accept/reject decision, reason, actor and time. |

### 3.10 Customers, loyalty and complaints

| Candidate table | Placement | Purpose / notes |
|---|---|---|
| `crm.Customers` | C authoritative; B restricted local projection | Customer identity/profile; unique verified phone policy. |
| `crm.CustomerPhoneVerifications` | C | OTP verification attempts/results; never store plaintext OTP values. |
| `crm.LoyaltyProgramRules` | C | Centrally managed earning, redemption, expiry and eligibility rules. |
| `crm.LoyaltyAccounts` | C authoritative; B restricted balance/eligibility projection | Customer points account and current governed balance. |
| `crm.LoyaltyPointTransactions` | C authoritative; B local event/projection only as required for Offline sales | Earn, redeem, expire, reverse and adjust points as immutable ledger entries. |
| `crm.CustomerComplaints` | C | Complaint reference, status and responsible employee. |
| `crm.ComplaintActions` | C | Chronological action/status history until closure. |

No reusable customer address-book table is proposed. Branch employees receive only the minimum customer data required for an assigned order and approved POS use.

### 3.11 Expenses, payroll tracking, cash deposits and maintenance

| Candidate table | Placement | Purpose / notes |
|---|---|---|
| `operations.ExpenseCategories` | C | Centrally managed expense types. |
| `operations.ExpenseRequests` | B request origin; C approval authority | Prospective branch expense request before purchase/payment. |
| `operations.ExpenseRequestDocuments` | C; branch upload origin | Quotation/pro-forma and supporting document references. |
| `operations.ExpenseApprovals` | C | Approve/reject/return decision, reason and actor. |
| `operations.ExpenseExecutions` | B execution evidence; C reviewed record | Actual amount, final invoice/payment proof and completion review. |
| `operations.PayrollTrackingRecords` | C | Placeholder for the centrally managed payroll-tracking scope only; exact fields and supporting entities remain deferred until the approved FIN-03 scope is mapped. No statutory calculation or payroll accounting is implied. |
| `operations.CashDeposits` | C | Central management of cash collected from branches and deposited/received centrally. |
| `operations.CashDepositLines` | C | Breakdown/source linkage for each deposit, such as branch/shift/collection references. |
| `maintenance.MaintainableAssets` | C master; B local reference | Devices/assets and branch locations subject to maintenance. |
| `maintenance.MaintenanceRequests` | B request origin; C workflow authority | Fault, urgency, asset, status and unique reference. |
| `maintenance.MaintenanceAssignments` | C | Assigned technician/service provider and assignment history. |
| `maintenance.MaintenanceWorkLogs` | C; B confirmation evidence | Technician report, work performed, cost and branch operational confirmation. |
| `maintenance.MaintenanceDocuments` | C references; upload may originate at B | Photo/video/report/document metadata and storage references. |

Payroll remains limited to the approved central tracking scope. It does not imply attendance, leave, statutory deductions, automated payroll calculation, or general-ledger accounting.

### 3.12 Alerts, audit and reporting

| Candidate table | Placement | Purpose / notes |
|---|---|---|
| `alerts.AlertDefinitions` | C | Centrally selected alert types, thresholds, channels and recipients policy. |
| `alerts.AlertEvents` | C authoritative; branch source event may originate at B | Actual alert instances and severity/context. |
| `alerts.AlertRecipients` | C | Recipients for each alert type/event. |
| `alerts.AlertDeliveries` | C | In-app/email/push delivery results. |
| `alerts.AlertAcknowledgements` | C | Recipient acknowledgement, note and action; does not delete alert history. |
| `audit.AuditEntries` | C and B local | Append-only actor/action/time/target/correlation records within each database boundary. |
| `audit.AuditChangeDetails` | C and B local | Before/after field details when required and safe to retain. |
| `audit.SecurityEvents` | C; B security events synchronized | Sign-in anomalies, device/security events and investigations. |
| `reporting.ReportRunRecords` | C, optional | Report request parameters, requester, time and output reference when report export/history is required. |
| `reporting.*ReadModels` | C, derived | Authorized central reporting projections. These are derived data, not alternate operational authorities. |

Reporting initially uses relational read models/projections. This inventory does not assume a separate data warehouse or analytics platform.

### 3.13 Offline and synchronization infrastructure

These are the five tables already specified in TD-12. They are infrastructure tables, not business-domain tables.

| Table | Placement |
|---|---|
| `sync.OutboxMessages` | C+B |
| `sync.InboxMessages` | C+B |
| `sync.ProcessedOperations` | C+B |
| `sync.BranchCommands` | C only |
| `sync.SyncCheckpoints` | C+B |

TD-12 is authoritative for their SQL Server physical design and ownership details. Do not create a second generic command-delivery table for the same BranchCommands purpose.

## 4. Important non-table decisions for the detailed model

The following should not automatically become tables:

- Statuses, state machines and small fixed enumerations: use stable code values unless they are centrally configurable reference data.
- Product, invoice, order, reservation, payment and refund status histories: use the owning aggregate's explicit history/event structure where required; avoid duplicate generic histories.
- Invoice number sequence: `InvoiceSequences` stores a counter, but the counter is not a primary key.
- Product images, expense attachments, maintenance photos/videos and delivery evidence: store file metadata and protected object/file references in SQL; do not default to storing large binary content in relational rows.
- Branch copies of central catalog/pricing/identity data: these are projections/snapshots, not independently editable master records.
- Report outputs and logs: persist only when there is a defined audit, retention or retrieval requirement.

## 5. Initial ownership summary

| Data area | Central DB | Each Branch DB |
|---|---|---|
| Organization, employees, roles, permissions | Authoritative | Minimum approved access/session/device/policy projection |
| Product catalog and product definitions | Authoritative | Read-only approved catalog projection |
| Suppliers and purchase orders | Authoritative | Receiving and return execution facts as assigned |
| Physical inventory, batches and movements | Consolidated projection/oversight | Authoritative |
| Pricing, promotions and coupons | Authoritative | Read-only approved versioned snapshot |
| POS invoices and shifts | Synchronized immutable history | Authoritative |
| POS payment evidence | Central reporting/reconciliation projection | Authoritative local transaction record |
| Online payment provider workflow | Authoritative | No provider authority; assigned order projection only |
| Online orders and reservation lifecycle | Authoritative coordinator | Assigned order + local stock reservation execution |
| Fulfillment and delivery | Assignment/policy/dispute authority | Picking, handover and delivery execution facts |
| Customers and loyalty | Authoritative | Restricted minimum projection/local events only as permitted |
| Expenses, payroll tracking and cash deposits | Approval/central authority | Request/execution/collection source evidence |
| Maintenance | Assignment/review/closure authority | Request and operational confirmation |
| Alerts and reports | Authoritative center and reporting | Local source events and branch-scoped operational views |
| Audit | Central actions and synchronized branch facts | Local operational/security facts |
| Durable messaging | Central command ledger and central streams | Local branch messaging state |

## 6. Coverage clarifications from approved BRS decisions

The following explicit workflows are included in the candidate table inventory and must be represented in the eventual ER model:

- Supplier invoices are registered by the Branch, matched centrally against the approved purchase order and actual received quantities (three-way match), approved by Central, and paid only by Central. Due-date reminders are generated centrally. This is not a general ledger or accounts-payable accounting module.
- Stocktake is centrally created and performed physically by Central employees at the branch. The Central system defines the scope, assignments, blind counts, recounts, per-item variance investigations and closure/cancellation reason. The Branch database enforces the scoped stock-movement freeze and records acknowledgements/authorized stock effects.
- Damaged/expired inventory is quarantined locally, submitted to Central for approval, then physically disposed by Branch staff with quantities, batches, executor, timestamp and evidence recorded.
- Customer returns are linked to the original invoice, inspected before any stock re-entry, and separately linked to refund/payment and loyalty adjustments. Exchanges are a linked return plus a separate replacement sale.
- Online delivery city/area, area-based fees, minimum order policy, pre-preparation cancellation requests and central decisions for picking unavailability are represented explicitly.
- Transfer governance and execution are central. The source/destination branch databases record only their own authorized physical stock movements and acknowledgements; they do not create cross-database foreign keys.
- Supplier-specific purchase prices are maintained centrally, while the actual cost of each received batch is stored independently on that branch's inventory batch.

## 7. Risks and questions intentionally deferred to table-level design

These are technical modeling tasks, not requests to reopen approved business policy:

1. Define exact entity boundaries and cardinalities, especially purchase receiving vs inventory batch creation, sale lines vs batch issues, returns vs inventory inspection, and online reservation coordination vs branch stock reservation.
2. Define whether a table is a true business aggregate, an append-only event, a projection, or an audit record; avoid storing the same fact as independently editable rows in multiple modules.
3. Define local transaction boundaries for POS completion, receiving, stock transfer dispatch/receipt, stocktake closure, payment/refund and website reservation acknowledgement.
4. Define concurrency controls for invoice sequence allocation, stock balance updates, batch issue, stock reservation, and payment/refund idempotency.
5. Define whether limited payroll needs any additional tables only after mapping the approved FIN-03/PAY-01 details; do not expand into payroll calculation.
6. Define retention, archival, privacy classification and file-storage references before implementation.
7. Resolve any discrepancy between the BRS index's historical review status and the owner-approved decision records by using the latest explicit owner-approved decisions; do not treat a document's old index label as a new business decision.

## 8. Next design sequence

1. Review and approve/correct this inventory and its Central/Branch ownership labels.
2. Produce the ER model and relationship/cardinality map for all modules.
3. Validate the full model against BRS requirement IDs and approved decision records.
4. Review PK/FK types, unique constraints, check constraints, delete behavior, indexes and concurrency boundaries.
5. Split the approved schema into dependency-ordered table groups for detailed design.
6. Produce EF Core entities/configurations only after the logical relationships and table ownership are approved.
7. Generate SQL migrations only after the physical schema and deployment topology are approved.

**No database table has been created by this document. No EF Core code, DDL, or migration is authorized by this inventory.**
