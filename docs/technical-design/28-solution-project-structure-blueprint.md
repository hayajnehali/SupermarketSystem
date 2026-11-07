# TD-28 — Solution and Project Structure Blueprint

**Version:** 0.1  
**Status:** Proposed for owner approval  
**Date:** 2026-11-07  
**Depends on:** Approved TD-01–TD-09, TD-27 (Clean Architecture and Pragmatic DDD), TD-03 (module boundaries), TD-04 (database ownership), and TD-26 (physical persistence standards).

## 1. Purpose and approval boundary

This document translates the approved architecture into a concrete Visual Studio solution/project map. It is a blueprint only: it does not create source code, entities, DbContexts, SQL tables, migrations, API endpoints or Angular applications.

The solution remains a **distributed Modular Monolith** with separate Central and Branch hosts. Clean Architecture and Pragmatic DDD apply within each host's modules. There is no project or deployment unit per business module.

## 2. Repository and solution layout

The proposed repository layout is:

```text
SupermarketSystem/
├── docs/
├── src/
│   ├── BuildingBlocks/
│   │   └── Supermarket.BuildingBlocks/
│   ├── Contracts/
│   │   └── Supermarket.Contracts/
│   ├── Central/
│   │   ├── Central.Domain/
│   │   ├── Central.Application/
│   │   ├── Central.Infrastructure/
│   │   ├── Central.Api/
│   │   └── Central.Worker/
│   └── Branch/
│       ├── Branch.Domain/
│       ├── Branch.Application/
│       ├── Branch.Infrastructure/
│       ├── Branch.Api/
│       └── Branch.Worker/
├── tests/
│   ├── Central.Domain.UnitTests/
│   ├── Central.Application.UnitTests/
│   ├── Central.Infrastructure.IntegrationTests/
│   ├── Branch.Domain.UnitTests/
│   ├── Branch.Application.UnitTests/
│   ├── Branch.Infrastructure.IntegrationTests/
│   ├── Architecture.Tests/
│   └── EndToEnd.Tests/
├── SupermarketSystem.sln
├── Directory.Build.props
├── Directory.Packages.props
└── README.md
```

Angular clients remain the three separately managed applications already approved in TD-01/TD-02: `supermarket-pos`, `supermarket-store`, and `supermarket-admin`. Their exact repository placement and workspace strategy are documented separately; this backend blueprint does not silently combine them into a new frontend architecture.

## 3. Project responsibilities and references

### 3.1 Shared projects

| Project | Contains | Must not contain |
|---|---|---|
| `Supermarket.BuildingBlocks` | Stable technical abstractions: clock, ID generator abstraction, current actor/device context contracts, result/error primitives and message metadata | Business entities, module rules, EF Core, ASP.NET Core hosting |
| `Supermarket.Contracts` | Versioned integration messages, synchronization envelopes/payload contracts and explicitly shared external contract DTOs | EF entities, Domain models, DbContexts, references to Central/Branch implementation projects |

Contracts must remain backward-compatible according to the synchronization/API versioning rules. A contract change is not a shortcut for sharing internal Domain types.

### 3.2 Central projects

| Project | Responsibility | Allowed project references |
|---|---|---|
| `Central.Domain` | Central-owned domain models, value objects, aggregates, domain policies and domain events, grouped by module | BuildingBlocks only when genuinely required |
| `Central.Application` | Central use cases, application ports, validation, authorization requirements, orchestration and module contracts | Central.Domain, BuildingBlocks, Contracts |
| `Central.Infrastructure` | Central EF Core mappings/context, repositories where justified, provider adapters, persistence, messaging adapters, file storage and technical implementations | Central.Application, Central.Domain, BuildingBlocks, Contracts |
| `Central.Api` | HTTP endpoints, authentication/authorization setup, API versioning, composition root and DI registration | Central.Application, Central.Infrastructure, Contracts |
| `Central.Worker` | Hosted background processes, command/message polling, synchronization orchestration entry points and composition root | Central.Application, Central.Infrastructure, Contracts |

### 3.3 Branch projects

| Project | Responsibility | Allowed project references |
|---|---|---|
| `Branch.Domain` | Branch-owned domain models, value objects, aggregates, stock/POS/fulfillment invariants and domain events | BuildingBlocks only when genuinely required |
| `Branch.Application` | Branch use cases, application ports, local authorization, transaction orchestration and module contracts | Branch.Domain, BuildingBlocks, Contracts |
| `Branch.Infrastructure` | Branch EF Core mappings/context, local persistence, outbox/inbox, provider/device adapters and technical implementations | Branch.Application, Branch.Domain, BuildingBlocks, Contracts |
| `Branch.Api` | Branch HTTP endpoints, local authentication/authorization validation, composition root and DI registration | Branch.Application, Branch.Infrastructure, Contracts |
| `Branch.Worker` | Local synchronization agent, central-command polling, durable acknowledgements and hosted background processes | Branch.Application, Branch.Infrastructure, Contracts |

### 3.4 Dependency direction

The permitted compile-time direction is:

```text
Central.Api ───────┐
                   ├──> Central.Infrastructure ──> Central.Application ──> Central.Domain
Central.Worker ────┘                 │                       │
                                    └───────────────────────┴──> BuildingBlocks / Contracts

Branch.Api ────────┐
                   ├──> Branch.Infrastructure ────> Branch.Application ────> Branch.Domain
Branch.Worker ────┘                 │                       │
                                    └───────────────────────┴──> BuildingBlocks / Contracts
```

- Domain must not reference Application, Infrastructure, API, Worker, EF Core or ASP.NET Core.
- Application must not reference Infrastructure, API, Worker or EF Core.
- Infrastructure implements Application ports; it does not own use-case decisions.
- API and Worker are composition roots and must not reference each other's host projects.
- Central projects must not reference Branch implementation projects; Branch projects must not reference Central implementation projects.
- Neither side references the other's DbContext or persistence entities.
- Shared projects must never reference Central or Branch projects.

## 4. Module folder layout

Modules are folders/namespaces inside the relevant layer projects, not separate projects.

Example Domain layout:

```text
Central.Domain/
├── OrganizationAccess/
├── ProductCatalog/
├── SupplierProcurement/
├── PricingPromotions/
├── PaymentsRefunds/
├── OnlineOrders/
├── CustomerLoyalty/
├── Fulfillment/
├── ExpenseMaintenance/
├── AlertsReporting/
└── SharedKernel/       # only if a real central-domain concept is justified
```

```text
Branch.Domain/
├── OrganizationAccess/
├── ProductCatalog/     # local approved projection concepts only
├── SupplierProcurement/
├── InventoryStock/
├── PricingPromotions/  # local approved snapshots/calculation only
├── PosSales/
├── PaymentsRefunds/
├── Fulfillment/
├── OfflineSynchronization/
└── SharedKernel/       # only if a real branch-domain concept is justified
```

The Application and Infrastructure projects use corresponding module folders. A module's folder exists only where that host has an approved responsibility; do not create empty duplicate modules merely to make the trees symmetrical.

Inside each module, organize by feature/use case when complexity warrants it (for example `CompleteSale`, `ReserveStock`, `ReceivePurchaseOrder`, `ProcessRefund`). Avoid a single global folder containing every command, handler, DTO or repository.

## 5. Module placement by host

| TD-03 module | Central | Branch |
|---|---|---|
| Organization & Access | Authority, administration, identity and policy | Local validation, device/branch context and approved snapshots |
| Product Catalog | Full master-data authority | Read-only approved projections |
| Supplier & Procurement | Supplier and purchasing authority/decisions | Receiving evidence and permitted local actions |
| Inventory & Stock | Approvals, consolidated views and transfer orchestration | Physical stock ledger and stock operations |
| Pricing & Promotions | Price/promotion authority and publication | Approved snapshots and local calculation |
| POS & Sales | Synchronized history/projections and reporting | POS sale, invoice, shift and drawer execution |
| Payments & Refunds | Online provider orchestration, callbacks and central coordination | Local POS tender results and authorized local execution |
| Online Store & Orders | Full order, checkout and reservation lifecycle | Assigned-order projection only where needed |
| Fulfillment & Delivery | Policy, assignment, monitoring and dispute decisions | Picking, packing, pickup/delivery execution evidence |
| Offline & Synchronization | Policy, inbound/outbound coordination and resumption authority | Local durable messaging and sync agent |
| Customer, Loyalty & Complaints | Customer, loyalty and complaint authority | Restricted local projection/use for approved workflows |
| Expense & Maintenance | Approval, assignment, review and closure | Request and local execution confirmation |
| Alerts & Reporting | Rules, recipients, central delivery and reporting | Local event production and authorized branch-scoped views |

This table refines project placement only; TD-03 remains the source for module responsibilities and TD-13 for table ownership.

## 6. EF Core and migration ownership

- Initial persistence registration uses one `CentralDbContext` in `Central.Infrastructure` and one `BranchDbContext` in `Branch.Infrastructure` per physical database.
- Each context maps only tables physically owned by that database according to TD-13 and the final approved physical schema.
- Entity type configurations are grouped by module under Infrastructure (for example `InventoryStock/Persistence/Configurations`).
- No Domain or Application project references EF Core.
- Separate migration assemblies are required: `Central.Infrastructure` for Central and `Branch.Infrastructure` for Branch, subject to final confirmation in the physical implementation plan.
- Migration startup projects are `Central.Api` and `Branch.Api` (design-time factories may be added if required for deterministic tooling).
- The exact DbContext schema/table mapping and migration commands are documented after the physical schema package is approved; do not generate an initial migration from this blueprint.

## 7. Dependency injection and host composition

- Each API/Worker host has a clear composition root.
- Infrastructure exposes explicit registration methods (for example `AddCentralInfrastructure` and `AddBranchInfrastructure`); Application exposes corresponding application registration methods.
- Do not scan every assembly indiscriminately. Registrations should be explicit or narrowly constrained by module.
- API and Worker may share the same Infrastructure/Application registrations for their site, but must configure only the services needed by that host.
- Secrets and provider credentials are configured outside source control and remain in the authority defined by TD-06/TD-07.

## 8. Test project boundaries

| Test project | Focus |
|---|---|
| Central.Domain.UnitTests | Central invariants and value objects |
| Central.Application.UnitTests | Central use-case orchestration and authorization |
| Central.Infrastructure.IntegrationTests | Central SQL mappings, constraints, concurrency and migrations |
| Branch.Domain.UnitTests | Branch stock/POS/fulfillment invariants |
| Branch.Application.UnitTests | Branch use cases and transaction intent |
| Branch.Infrastructure.IntegrationTests | Branch SQL mappings, concurrency, stock and sync persistence |
| Architecture.Tests | Dependency direction, forbidden references and module boundaries |
| EndToEnd.Tests | Critical cross-host workflows, provider callbacks and synchronization recovery |

Tests must not use EF Core InMemory as a substitute for SQL Server integration/concurrency behavior. Unit tests may use fakes for ports; persistence behavior is verified against SQL Server-compatible integration infrastructure.

## 9. Solution-wide conventions

- Target framework: `net10.0`, consistent with approved TD-02.
- Nullable reference types enabled.
- Implicit usings enabled.
- Central package version management through `Directory.Packages.props`.
- Shared compiler/build conventions through `Directory.Build.props`.
- Treat warnings as errors after the baseline build is clean; introduce analyzers incrementally and consistently.
- Use async APIs with cancellation tokens for I/O paths.
- Use structured logging and correlation/operation IDs; never log secrets, OTP plaintext or payment credentials.
- Configuration is environment-based; no connection strings or secrets committed.
- Naming and physical mapping follow TD-26 and the approved detailed schemas.

## 10. Decisions intentionally deferred

This blueprint does not silently decide the remaining TD-24 gates: exact Identity table mapping, payment terminal/provider topology, provider idempotency and callback ordering, Offline promotion/loyalty quota mechanics, supplier-payment allocation, JOD half-rounding tie-breaker, promotion operator catalogue, recovery command payloads, concurrency algorithms, retention/privacy, payroll detail and reporting calculation lineage.

Those are resolved in their relevant technical design documents before the affected feature or physical schema is implemented. The approved business policies remain unchanged.

## 11. Next step after approval

1. Approve TD-28 project names, responsibilities and dependency rules.
2. Close the implementation-impacting TD-24 gates in focused technical documents.
3. Finalize physical mappings and separate Central/Branch migration plans.
4. Create the empty .NET solution and projects, add architecture tests, and verify a clean Visual Studio build.
5. Only then implement the first vertical slice and create migrations after its physical schema is approved.

**No project code, database objects or migrations are created by this document.**
