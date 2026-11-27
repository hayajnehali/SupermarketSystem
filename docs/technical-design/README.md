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
| [TD-06 — Identity, Authentication, Authorization, and Security](06-identity-authentication-authorization-security.md) | Identity security domains aligned with TD-29 unified account persistence, Offline login, RBAC, devices and service authentication | Approved by owner |
| [TD-07 — API Boundaries and External Integrations](07-api-boundaries-and-external-integrations.md) | Branch/Central API ownership, independent POS bank-terminal workflow, online payment/OTP integrations, webhooks and API security | Approved by owner |
| [TD-08 — Deployment, Infrastructure, Backup, and Disaster Recovery](08-deployment-infrastructure-backup-disaster-recovery.md) | Central/branch deployment, networking, SQL operations, backups, RPO/RTO, recovery, release and monitoring | Approved by owner (2026-11-06) |
| [TD-09 — Detailed API Contracts](09-api-contracts-reservation-and-sync.md) | Customer reservation/status APIs, Branch.Worker command polling and acknowledgements, transaction boundaries, idempotency, security scopes and compatibility | Approved by owner (2026-11-06) |
| [TD-10 — Durable Messaging Data Model](10-durable-messaging-data-model.md) | Logical Outbox/Inbox/ProcessedOperations, BranchCommands, checkpoints, uniqueness, indexes, transaction boundaries and worker leases | Proposed for owner review |
| [TD-11 — Worker Execution and Recovery](11-worker-execution-and-recovery.md) | Central/Branch worker polling, atomic leases, retry classification, idempotent crash recovery, ordering, shutdown and observability | Approved by owner (2026-11-06) |
| [TD-12 — SQL Server Durable Messaging Schema](12-sql-server-durable-messaging-schema.md) | SQL Server physical table mapping, keys, filtered indexes, constraints, rowversion, transaction boundaries and EF Core mapping requirements | Approved by owner (2026-11-06) |
| [TD-13 — Complete Database Table Inventory](13-complete-database-table-inventory.md) | Full candidate table catalogue updated with unified Identity user store and exclusive Employee/Customer profile links | Approved by owner (2026-11-28) |
| [TD-14 — Logical Database Relationships](14-logical-database-relationships.md) | Logical cardinalities updated for unified auth.Users and mutually exclusive Employee/Customer profiles | Approved by owner (2026-11-28) |
| [TD-15 — Organization and Access Detailed Schema](15-organization-access-detailed-schema.md) | Organization/access schema updated with auth.Users, Employee profile FK, sessions and Identity integration | Approved by owner (2026-11-28) |
| [TD-16 — Product Catalog Detailed Schema](16-product-catalog-detailed-schema.md) | Table-level specification for products, categories, brands, barcodes, selling units, conversions, composites, attributes and media | Approved by owner (2026-11-28) |
| [TD-17 — Supplier and Procurement Detailed Schema](17-procurement-detailed-schema.md) | Table-level specification for suppliers, requests, purchase orders, receiving, supplier invoices, three-way matching, payments and returns | Approved by owner (2026-11-28) |
| [TD-18 — Inventory and Stock Detailed Schema](18-inventory-stock-detailed-schema.md) | Table-level specification for branch stock ledger, batches, balances, reservations, stocktaking, transfers, quarantine and disposal | Approved by owner (2026-11-28) |
| [TD-19 — Pricing, Promotions and Coupons Detailed Schema](19-pricing-promotions-and-coupons.md) | Table-level specification for unit prices, price/promotion versions, promotions, conditions, rewards, coupons and redemption limits | Approved by owner (2026-11-28) |
| [TD-20 — POS, Sales, Returns and Exchanges Detailed Schema](20-pos-sales-returns-and-exchanges.md) | Table-level specification for shifts, drawers, suspended carts, invoices, batch issues, cancellation, returns, inspection and exchanges | Approved by owner (2026-11-28) |
| [TD-21 — Payments and Refunds Detailed Schema](21-payments-refunds-detailed-schema.md) | Payment methods, independent POS bank-terminal cashier confirmation, online provider transactions, cash-only refunds, Central approval and reconciliation | Approved by owner (2026-11-28) |
| [TD-22 — Online, Customer, Loyalty and Fulfillment Detailed Schema](22-online-crm-fulfillment-detailed-schema.md) | Customer profile schema updated with unique auth.Users link; online orders, loyalty and fulfillment | Approved by owner (2026-11-28) |
| [TD-23 — Operations, Maintenance, Alerts, Audit and Reporting](23-operations-maintenance-alerts-audit-reporting-schema.md) | Table-level specification for expense approvals, cash deposits, maintenance, alert workflows, audit/security history and Central reports | Approved by owner (2026-11-28) |
| [TD-24 — Comprehensive Database Schema Consistency Review](24-comprehensive-database-schema-consistency-review.md) | Consistency review updated for TD-29 identity mapping; records remaining SQL enforcement and security gates | Approved by owner (2026-11-28) |
| [TD-25 — Module-Level ERD Package](25-module-level-erd-package.md) | Module ERDs updated with unified Identity user and exclusive Employee/Customer profile relationships | Approved by owner (2026-11-28) |
| [TD-26 — SQL Server Physical Schema Standards](26-sql-server-physical-schema-standards.md) | SQL Server 2025 data types, Guid keys, constraints, indexes, concurrency, EF Core 10 mappings and migration standards | Approved by owner (2026-11-28) |
| [TD-27 — Clean Architecture and Pragmatic DDD](27-clean-architecture-and-pragmatic-ddd.md) | Dependency direction, Domain/Application/Infrastructure/hosts, pragmatic DDD, project boundaries and testing rules | Approved by owner (2026-11-28) |
| [TD-28 — Solution and Project Structure Blueprint](28-solution-project-structure-blueprint.md) | Exact .NET solution layout, Central/Branch project responsibilities, module folders, dependency rules, EF Core and test boundaries | Approved by owner (2026-11-28) |
| [TD-29 — Unified Identity Account Model](29-identity-persistence-and-separation.md) | One ASP.NET Core Identity user store; exclusive Employee/Customer account type; separate business profiles; role and Offline boundaries | Approved by owner (2026-11-08) |\n| [TD-30 — Final Technical Decisions](30-final-technical-decisions.md) | Eight owner-approved decisions covering Offline Identity, stock/refund concurrency, Offline promotions, supplier payments, rounding, promotion stacking and data retention | Approved by owner (2026-11-28) |
| [TD-31 — Owner-Approved Implementation Contracts](31-owner-approved-implementation-contracts.md) | Provider idempotency, split-tender refund allocation, online cash refund handover, Offline promotion/loyalty, supplier allocations, rounding, recovery, concurrency, POS and reporting contracts | Approved by owner (2026-11-28) |

## Reviews

- [Comprehensive Technical Design Review — 2026-11-06](reviews/2026-11-06-comprehensive-technical-design-review.md) — cross-document consistency, unresolved design risks, approval status and readiness actions.

## Next design sequence

1. Produce the complete Central physical schema from the owner-approved logical inventory: exact table placement, columns, SQL types/lengths, PK/FK, unique/filtered indexes, check constraints, delete behavior and transaction/concurrency requirements.
2. Produce the complete Branch physical schema independently, including local operational tables, approved Central projections and the five durable synchronization tables where applicable.
3. Cross-check Central/Branch GUID correlations, API/Outbox/Inbox contracts, schema-version compatibility and absence of cross-database SQL foreign keys.
4. Review and approve the two physical schemas separately, then freeze the combined physical baseline.
5. Complete the security design for the Offline credential verifier and provider-specific adapter contracts before implementing those affected features; validate retention periods and legal/privacy requirements before production.
6. Only after physical schema approval, create the empty .NET solution/projects under TD-27/TD-28, architecture tests, Central/Branch DbContext configurations and separate migration plans.
7. Generate SQL migrations/scripts only after the physical schema is approved; validate with SQL Server integration, concurrency, backup and recovery tests.

TD-12 covers only the five durable synchronization infrastructure tables; it is not the complete supermarket database. No production SQL tables or migrations have been created by the design documents. only the five durable synchronization infrastructure tables; it is not the complete supermarket database. No production SQL tables or migrations have been created by the design documents.
