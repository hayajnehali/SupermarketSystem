# Technical Design

**Status:** Technical baseline review completed — owner decisions and implementation gaps recorded  
**Source of truth:** Merged BRS and approved discovery decisions.  
**Rule:** Technical documents may detail implementation choices but must not alter approved business requirements.

## Deliverables

| Document | Coverage | Status |
|---|---|---|
| [TD-01 — Architecture Overview](01-architecture-overview.md) | Distributed modular-monolith topology, Central/Branch APIs and Workers, Angular clients, data authority, synchronization principles | Approved by owner (2026-11-06) |
| [TD-02 — Recommended Technology Stack](02-recommended-technology-stack.md) | C#, .NET 10, ASP.NET Core, EF Core, SQL Server 2025, Angular 22, TypeScript 6, Node.js 26 | Approved by owner (2026-11-06) |
| [TD-03 — Backend Modules and Dependencies](03-backend-modules-and-dependencies.md) | Business modules, Central/Branch deployment map, ownership, dependency and workflow boundaries | Approved by owner (2026-11-06) |
| [TD-04 — Database Topology and Data Ownership](04-database-topology-and-data-ownership.md) | Central/branch databases, authoritative data ownership, transaction boundaries, stock/order/payment consistency | Approved by owner (2026-11-06) |
| [TD-05 — Central/Branch Synchronization](05-central-branch-synchronization.md) | Outbox/Inbox, idempotency, message flow, checkpoints, retries, conflicts, Offline resumption gate, owner-approved Branch.Worker command polling and online reservation state machine | Approved by owner (2026-11-06) |
| [TD-06 — Identity, Authentication, Authorization, and Security](06-identity-authentication-authorization-security.md) | Employee/customer identities, Offline login, RBAC, branch isolation, POS devices, service authentication, data protection and audit | Approved by owner |
| [TD-07 — API Boundaries and External Integrations](07-api-boundaries-and-external-integrations.md) | Branch/Central API ownership, critical workflows, payment/OTP integrations, webhooks, versioning and API security | Approved by owner |
| [TD-08 — Deployment, Infrastructure, Backup, and Disaster Recovery](08-deployment-infrastructure-backup-disaster-recovery.md) | Central/branch deployment, networking, SQL operations, backups, RPO/RTO, recovery, release and monitoring | Approved by owner (2026-11-06) |
| [TD-09 — Detailed API Contracts](09-api-contracts-reservation-and-sync.md) | Customer reservation/status APIs, Branch.Worker command polling and acknowledgements, transaction boundaries, idempotency, security scopes and compatibility | Approved by owner (2026-11-06) |
| [TD-10 — Durable Messaging Data Model](10-durable-messaging-data-model.md) | Logical Outbox/Inbox/ProcessedOperations, BranchCommands, checkpoints, uniqueness, indexes, transaction boundaries and worker leases | Proposed for owner review |
| [TD-11 — Worker Execution and Recovery](11-worker-execution-and-recovery.md) | Central/Branch worker polling, atomic leases, retry classification, idempotent crash recovery, ordering, shutdown and observability | Approved by owner (2026-11-06) |
| [TD-12 — SQL Server Durable Messaging Schema](12-sql-server-durable-messaging-schema.md) | SQL Server physical table mapping, keys, filtered indexes, constraints, rowversion, transaction boundaries and EF Core mapping requirements | Approved by owner (2026-11-06) |
| [TD-13 — Complete Database Table Inventory](13-complete-database-table-inventory.md) | First-pass full table catalogue, Central/Branch ownership, projections and database design sequence | Proposed for owner review |
| [TD-14 — Logical Database Relationships](14-logical-database-relationships.md) | Conceptual parent/child cardinalities, local FK boundaries, cross-site correlations and transaction boundaries | Proposed for owner review |
| [TD-15 — Organization and Access Detailed Schema](15-organization-access-detailed-schema.md) | First table-level specification for branches, employees, assignments, RBAC, devices and branch access snapshots | Proposed for owner review |
| [TD-16 — Product Catalog Detailed Schema](16-product-catalog-detailed-schema.md) | Table-level specification for products, categories, brands, barcodes, selling units, conversions, composites, attributes and media | Proposed for owner review |
| [TD-17 — Supplier and Procurement Detailed Schema](17-procurement-detailed-schema.md) | Table-level specification for suppliers, requests, purchase orders, receiving, supplier invoices, three-way matching, payments and returns | Proposed for owner review |
| [TD-18 — Inventory and Stock Detailed Schema](18-inventory-stock-detailed-schema.md) | Table-level specification for branch stock ledger, batches, balances, reservations, stocktaking, transfers, quarantine and disposal | Proposed for owner review |
| [TD-19 — Pricing, Promotions and Coupons Detailed Schema](19-pricing-promotions-detailed-schema.md) | Table-level specification for unit prices, price/promotion versions, promotions, conditions, rewards, coupons and redemption limits | Proposed for owner review |
| [TD-20 — POS, Sales, Returns and Exchanges Detailed Schema](20-pos-sales-returns-detailed-schema.md) | Table-level specification for shifts, drawers, suspended carts, invoices, batch issues, cancellation, returns, inspection and exchanges | Proposed for owner review |
| [TD-21 — Payments and Refunds Detailed Schema](21-payments-refunds-detailed-schema.md) | Table-level specification for providers, payment methods/transactions, attempts, trusted events, refunds, exceptions and reconciliation | Proposed for owner review |
| [TD-22 — Online, Customer, Loyalty and Fulfillment Detailed Schema](22-online-crm-fulfillment-detailed-schema.md) | Table-level specification for customers, carts/orders, reservations, delivery, loyalty, complaints, picking, failed delivery, disputes and pickup retention | Proposed for owner review |
| [TD-23 — Operations, Maintenance, Alerts, Audit and Reporting](23-operations-maintenance-alerts-audit-reporting-schema.md) | Table-level specification for expense approvals, cash deposits, maintenance, alert workflows, audit/security history and Central reports | Proposed for owner review |
| [TD-24 — Comprehensive Database Schema Consistency Review](24-comprehensive-database-schema-consistency-review.md) | Cross-document verification of table inventory, ownership, Guid PKs, FK boundaries, ERD baseline and remaining technical gates | Proposed for owner review |

## Reviews

- [Comprehensive Technical Design Review — 2026-11-06](reviews/2026-11-06-comprehensive-technical-design-review.md) — cross-document consistency, unresolved design risks, approval status and readiness actions.

## Next design sequence

1. Publish a complete database inventory derived from the approved BRS, module map, and data-ownership rules.
2. Assign every proposed table to Central DB, each Branch DB, or both; identify replicated projections separately from authoritative data.
3. Define primary keys (`Guid` only), foreign keys, unique constraints, check constraints, and cross-module ownership boundaries.
4. Review the complete entity relationship map for omissions, duplication, cycles, and cross-database FK violations.
5. Design table groups in dependency order, beginning with organization/access and product catalog, then inventory, pricing, procurement, POS/sales, payments, online orders/fulfillment, loyalty, audit, and reporting.
6. Only after logical schema approval, produce SQL physical details and EF Core entity/configuration designs, then migrations and integration tests.

TD-10 through TD-12 are synchronization infrastructure design documents; they do not represent the complete supermarket database. No application migrations should be generated before the complete logical schema is approved.
