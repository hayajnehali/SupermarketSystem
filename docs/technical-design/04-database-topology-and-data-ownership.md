# Technical Design — Database Topology and Data Ownership

**Document:** TD-04  
**Version:** 0.1  
**Status:** Approved by project owner — data ownership baseline accepted (2026-11-06)  
**Depends on:** Merged BRS, TD-01 Architecture Overview, TD-03 Backend Modules.

## 1. Database topology

Use one logically central SQL Server database and one logically separate SQL Server database per branch.

- Central database stores centrally governed records, online orders, central approvals, authoritative configuration, central payment state, synchronization coordination, and consolidated reporting projections.
- Each branch database stores that branch's operational stock ledger, POS transactions, invoice sequences, cashier shifts, branch receiving/fulfillment activity, local approved master-data snapshots, and durable synchronization queues.
- Branch databases are isolated from one another. Branch A does not query or update Branch B's database.
- Central and branch applications communicate through authenticated APIs and synchronization messages, never by direct cross-database SQL access.
- The logical database boundary does not require a separate physical SQL Server machine per branch; physical deployment and edition sizing remain separate decisions. Each branch must nevertheless have its own isolated database and local application availability.

## 2. Data ownership model

Ownership means the application context authorized to create and change the authoritative record. A read-only copy or projection at another location does not transfer ownership.

| Data group | Authoritative owner | Local/remote copies | Rules |
|---|---|---|---|
| Branch and employee administration, roles and permissions | Central | Branch authorization snapshot | Branch validates only the last approved snapshot within the approved Offline/login validity rules. |
| Product catalog, categories, SKU/barcodes, units and composition | Central | Approved branch catalog projection | Branch cannot change central product definitions. |
| Supplier, purchasing governance, supplier terms and approvals | Central | Branch receives approved purchase/receipt tasks | Branch records only its permitted receipt evidence; central owns purchasing decisions. |
| Physical stock, batches and stock movements | Each branch, for its own stock | Central synchronized projection | Central controls approved adjustments/transfers; physical branch balances are never inferred from another branch. |
| Prices, promotions, coupons and effective dates | Central | Approved branch price/promotion snapshot | Branch uses the latest approved local copy within Offline policy; expired promotion falls back to approved base price. |
| POS sales, invoices, shifts and cash drawer operations | Branch that executed the sale | Central immutable synchronized transaction record/projection | Invoice number is generated under that branch's sequence; completed invoice snapshot is preserved. |
| Branch POS tender result | Branch POS transaction context, subject to provider confirmation | Central synchronized payment/financial record | No sale completes while required payment is pending, failed or unconfirmed. |
| Online order, stock reservation lifecycle and online payment state | Central | Assigned branch receives fulfillment projection/task | One order belongs to one selected branch; the central order lifecycle remains authoritative. |
| Online fulfillment, picking, handover and delivery attempt evidence | Assigned branch records execution | Central order history projection | Branch cannot substitute unavailable products or decide a customer dispute/refund. |
| Online payment-provider events and refunds | Central | Assigned branch receives only relevant fulfillment/status updates | Provider callbacks are validated centrally; duplicate events are idempotent. |
| Customer profile, phone verification, loyalty and complaints | Central | Restricted branch cache/projection only where approved for POS/order execution | Branch and delivery staff cannot browse/search customer profiles. |
| Expense requests and maintenance execution evidence | Branch submits evidence; Central owns approval and closure | Central authoritative workflow record; branch status projection | Branch cannot purchase/pay before approval or close central workflows. |
| Alerts and central reports | Central | Branch-scoped operational status only where permitted | Sales/profit/inventory reports remain Central Administration only. |
| Audit events | Each application records events for its own actions; central audit context consolidates synchronized records | Durable local audit plus central synchronized history | Preserve actor, branch/device context, timestamp, operation/correlation IDs, decision/reason, and before/after values where applicable. |

## 3. Data placement by application

### 3.1 Central database

Logical data areas:
- Organization, branches, employees, accounts, roles, permissions and device registry.
- Product catalog, category tree, selling units, barcodes, composite definitions and product media metadata.
- Suppliers, purchasing workflows, purchase orders, central approvals and supplier terms.
- Central price lists, promotions, coupons and publication versions.
- Online customer accounts, OTP verification state, loyalty ledger and complaints.
- Online orders, order lines, branch assignment, reservations, online payment attempts, provider events and refunds.
- Delivery policy, central dispute decisions, expenses, maintenance workflows, alert configuration and central reporting projections.
- Synchronization endpoints/branch registry, message receipts, acknowledgements, conflict cases and central resumption approvals.
- Central audit records and backup/restore metadata.

### 3.2 Branch database

Logical data areas:
- Branch-scoped employee authorization snapshot, POS device registration snapshot and branch configuration.
- Read-only approved product, unit, barcode, composition, price and promotion snapshots with publication/version metadata.
- Branch stock ledger, stock balances, batches, batch costs, expiry data, stock reservations received from Central, quarantine and inspection states.
- Local purchase receiving evidence and branch stock operations allowed by the BRS.
- POS sales, invoice headers/lines, immutable invoice value snapshots, invoice sequence/year, payment allocations, refunds/returns executed locally, cashier shifts and cash drawer records.
- Assigned online-order fulfillment projection, picking/packing results, handover evidence and the single delivery attempt result.
- Local outbox, inbound message receipts, synchronization checkpoints, retry state, conflict references and audit records.

Branch database contents must be branch-scoped. Customer information is minimized to what an assigned order or an approved POS workflow requires.

## 4. Special consistency rules

### 4.1 Stock and reservations

1. Each branch's stock ledger is authoritative for its physical stock.
2. Central online ordering may rely only on the latest accepted branch stock/availability information and must not treat stale branch data as current proof of stock.
3. When an online order is confirmed, Central creates the 15-minute reservation lifecycle and sends the reservation/fulfillment instruction to the selected branch through the approved application/synchronization contract.
4. POS must exclude stock reserved for website orders, quarantined stock, expired stock, and any other non-sellable quantity.
5. The branch records actual picking and inventory movements. Central order status is updated from acknowledged branch events.
6. A branch outage prevents new website orders being assigned to that branch.
7. No stock is shared or transferred implicitly between branch databases.

The exact reservation handshake, branch acknowledgement timeout, and stock freshness threshold require a dedicated synchronization design decision. This document does not invent those timings.

**Owner-approved stock-concurrency invariant (2026-11-06):** The assigned Branch database is the final authority for accepting an online stock reservation and for completing a POS sale against that branch's physical stock. Reservation processing and POS sale validation must serialize against the same branch stock/reservation state. The branch must persist an accepted reservation durably before acknowledging it to Central. Therefore, the same stock units cannot be both reserved for a website order and sold through POS. The specific SQL locking/isolation or conditional-update technique remains an implementation detail for the detailed database design; this approval does not select one.

### 4.2 Sales and invoices

- The branch creates and completes its POS invoice locally.
- Invoice numbering uses branch code, sequence year, and branch/year sequence, with the approved format `BR01-YYYY-000001` and the `SequenceYear` field.
- Invoice completion, stock movement, payment allocation, and outbox event creation must be committed consistently within the branch database boundary.
- A retry must not create a second invoice, stock deduction, or payment allocation for the same operation.
- The central copy is a synchronized historical record; later central master-data changes must not rewrite the completed invoice.

### 4.3 Online orders and payments

- Central owns the online order and online payment lifecycle.
- The 15-minute reservation/payment window is controlled centrally.
- A late payment against a cancelled order is recorded against that order and does not reopen it or re-reserve stock.
- Provider callback processing, refund status, and financial idempotency are centralized.
- Branch receives only the order data and customer details needed to execute its assigned fulfillment task.

### 4.4 Offline and synchronization

- Each branch durably records local operations before attempting to synchronize them.
- Central and branch maintain durable message receipt/checkpoint state to support retries and duplicate detection.
- Stable OperationId identifies a business operation across retry and delivery attempts.
- Centrally governed data flows from Central to branches as approved versioned snapshots.
- Branch operational events flow to Central as immutable business facts with source branch and actor/device context.
- Central is authoritative for centrally managed data. Branch is authoritative for the local physical stock and local POS event it executed, subject to central review/approved adjustments.
- Conflicts are isolated and referred to Central Administration; they are not silently overwritten.
- Valid unrelated operations continue to synchronize when one operation conflicts.
- Sales resume only after the approved synchronization, reconciliation, refresh, and explicit central authorization conditions are satisfied.

## 5. Data integrity and transaction boundaries

- Use database transactions for operations that must be atomic within one database.
- A branch POS sale must not be split across a central database transaction and a branch database transaction.
- Do not assume a distributed transaction across Central and Branch databases.
- Use transactional Outbox for committed operations that must be published.
- Use durable Inbox/processed-message records for idempotent consumption.
- Enforce unique constraints for business identifiers such as branch/year invoice numbers, OperationId processing, and customer phone number.
- Use foreign keys and database constraints for local relational integrity where applicable.
- Use explicit status transitions and concurrency checks for stock reservations, orders, payment attempts, refunds, and approval workflows.
- Historical financial and invoice values are snapshots and are not recomputed from current master data.

## 6. Retention and deletion principles

- Branch suspension must not delete historical invoices, stock movements, or other operational history.
- Completed invoices and financial records are retained as immutable historical records.
- Audit, synchronization, provider callback, and message-processing histories must remain available for authorized investigation according to a retention policy to be specified before implementation.
- Physical deletion of records involved in financial, inventory, approval, or audit history is not part of ordinary workflows.
- Exact statutory retention periods are not specified here; tax/e-invoicing remain future-readiness placeholders under the approved BRS.

## 7. Data access and privacy

- Angular applications access data only through authorized APIs.
- Every branch request is scoped to its branch and the active authorized employee/device context.
- Central administration access follows central permissions and audit.
- Branch and delivery employees cannot perform general customer searches.
- Assigned-order projections contain only the customer details needed for fulfillment and delivery.
- Secrets, provider credentials, and authentication tokens are not stored in plaintext business tables or exposed to Angular clients.
- Central and branch databases are not exposed directly to public networks.

## 8. Still to be decided in detailed database design

1. Exact table/entity catalogue and relationships.
2. Branch stock ledger event model and balance projection strategy.
3. Online reservation handshake, branch acknowledgement, and stock freshness thresholds.
4. Exact synchronization message schema, versioning, batch size, and checkpoint strategy.
5. Concurrency/locking strategy for simultaneous POS sales and online reservations.
6. SQL Server editions, sizing, backup frequency/retention, and physical placement.
7. Indexing, partitioning, archival, and reporting projection strategy.
8. Detailed data-retention schedule and privacy/security classification.

## 9. Approval boundary

TD-04 defines logical database ownership and consistency boundaries. It does not prescribe final SQL tables, columns, indexes, stored procedures, physical servers, or authorize implementation.
