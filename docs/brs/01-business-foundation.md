# Business Requirements Specification (BRS)
## Supermarket Management System

**Document version:** 0.1  
**Part:** 1 — Business foundation and scope  
**Status:** Approved by the project owner for documentation  
**Repository:** `hayajnehali/SupermarketSystem`

---

## 1. Purpose

Establish a centralized system to manage and operate a multi-branch supermarket chain. The central administration controls policies, prices, master data, employees, permissions, and centrally approved operations. Branches execute operational activities within their assigned permissions.

The system covers:

- Central and branch administration.
- Products, categories, barcodes, and selling units.
- Suppliers, purchasing, and receiving.
- Inventory, batches, stocktaking, and costing.
- Prices, promotions, and discounts.
- POS, invoices, and cashier shifts.
- Payments, refunds, and returns.
- Online store and customer orders.
- Order preparation, delivery, and pickup.
- Offline operation and synchronization.
- Customers, loyalty, coupons, and complaints.
- Expenses, limited payroll, maintenance, alerts, and reports.

## 2. Business objectives

1. Centralize management of policies, prices, and master data.
2. Provide central visibility into branch performance, inventory, and sales.
3. Control sales, purchasing, receiving, and stocktaking processes.
4. Preserve accurate inventory costing and profit calculations.
5. Support sales through branches and the online store.
6. Reduce errors and misuse through permissions and audit trails.
7. Maintain operational continuity under the approved offline policy.
8. Provide reliable information for management decisions.

## 3. Scope

### 3.1 In scope

- Branch, employee, role, and permission management.
- Product, barcode, selling-unit, and composite-product management.
- Supplier, purchase-request, receiving, and supplier-return processes.
- Inventory, batches, costing, stocktaking, and inter-branch transfers.
- Central price, promotion, and coupon management.
- POS, invoices, and cashier shifts.
- Cash and electronic payments, refunds, and returns.
- Online store, customer orders, preparation, delivery, and pickup.
- Customer accounts, loyalty points, and complaints.
- Expense management, limited payroll tracking, and maintenance.
- Central alerts and reports.
- Offline operation and synchronization.
- Audit, backup, and account-security controls.

### 3.2 Out of scope for the current release

- Wholesale sales.
- Credit sales and customer debt management.
- A complete accounting system or general ledger.
- Automated payroll calculation under labor, social-security, or tax legislation.
- Attendance, leave, and time tracking for payroll calculation.
- Bulk bank payroll transfers.
- Full tax and e-invoicing integration; current requirements provide only for future-readiness placeholders.
- Splitting a single online order across multiple branches.

## 4. Organizational and operational model

### 4.1 Central administration

The central administration is the primary administrative authority and is responsible for:

- Creating branches, changing their data, activating, and suspending them.
- Managing employee accounts, roles, and permissions.
- Managing products, suppliers, and purchasing.
- Managing prices, promotions, and business policies.
- Approving operations that require central authorization.
- Monitoring branches, synchronization, and conflicts.
- Managing payroll, expenses, and cash deposits.
- Monitoring reports, maintenance, and alerts.

### 4.2 Branches

Branches are operational units. They execute permitted activities within centrally defined policies and permissions. A branch cannot independently change central policies, prices, or permissions.

### 4.3 Core roles

The core business roles are:

- Central Admin
- Branch Manager
- Cashier
- Inventory Clerk
- Pricing Clerk

The Branch Manager title does not grant independent administrative or operational authority. A Branch Manager, like every branch employee, may perform only actions explicitly granted by Central Administration; centrally controlled decisions and approvals remain central.

Permissions are managed centrally. Additional permissions may be granted to or withdrawn from an employee without changing their role.

A Branch Manager cannot independently create user accounts or administer permissions. The manager may submit a request to the central administration.

## 5. General business rules

### 5.1 Centralized governance

- The central administration alone manages policies, prices, promotions, suppliers, employees, and permissions.
- Branches execute operations within their assigned permissions.
- A branch cannot approve an operation that requires central approval.

### 5.2 Branches and inventory

- Each branch maintains independent inventory.
- The system must not allow a sale of quantities that are not actually available.
- Suspending a branch must not delete its historical data.
- Inter-branch transfers require an approved request. Quantity is deducted when dispatched and added when physically received.

### 5.3 Products and costing

- Each distinct size or type is represented by a separate SKU.
- The system does not use average costing.
- Cost of goods sold is calculated from the actual inventory batches issued.
- The cost recorded for a completed sale is historical and must not change when later purchase costs change.
- FEFO applies to products requiring expiry tracking; FIFO applies to other products.
- Expired products must not be sold.

### 5.4 Sales and payments

- Credit sales are not permitted.
- Cashiers cannot manually override product prices.
- A sale cannot be completed using a pending or unconfirmed payment.
- A single POS invoice may be paid using multiple payment methods, provided the full invoice total is paid.

### 5.5 Approvals and audit

- Operations requiring central approval cannot be approved by a branch.
- Important operations must record the actor, timestamp, and relevant changes.
- Historical records linked to financial or operational transactions must not be physically deleted.

### 5.6 Online store

- Online orders require electronic payment.
- The customer selects the responsible branch.
- A single order cannot be split across multiple branches.
- New orders are not accepted for an offline branch.
- Product reservation during checkout is 15 minutes.
- Guest checkout is not supported.
- The central administration does not recreate a cancelled order on the customer's behalf. The customer may place a new order independently.

## 6. Authority and responsibility

The system must distinguish, where applicable, between:

1. Creating an operation.
2. Executing an operation.
3. Reviewing an operation.
4. Approving an operation.

Separation of duties must be applied to sensitive financial, inventory, and administrative operations according to the approved business rules.

## 7. Decision references and traceability

This part is based on the approved project decision log and meeting minutes, together with business recommendations approved by the project owner during the BRS review.

Some detailed approvals, including P1–P40, PAY-01 (limited payroll), and FIN-04 (branch cash deposits), were approved during the conversation but had not yet been transcribed into the repository decision records at the time this part was prepared. They must be linked to requirement identifiers and recorded in the project decision history as part of the documentation reconciliation.

## 8. Document status and limitations

This file documents only Part 1 of the BRS: the business foundation, scope, organizational model, and shared business rules. It is not the complete BRS. Functional requirements, use cases, exception scenarios, non-functional requirements, and testable acceptance criteria will be documented in subsequent parts and reviewed separately before the BRS is considered complete.
