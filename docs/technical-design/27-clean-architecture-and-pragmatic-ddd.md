# TD-27 — Backend Code Architecture: Clean Architecture and Pragmatic DDD

**Version:** 0.1  
**Status:** Proposed for owner approval  
**Date:** 2026-11-07  
**Depends on:** Approved TD-01, TD-02, TD-03, TD-04, TD-05, TD-06, TD-09, and logical database package TD-13–TD-26.

## 1. Decision requested

Adopt **Clean Architecture with Pragmatic Domain-Driven Design (DDD)** inside the approved distributed Modular Monolith.

This decision governs source-code organization and dependency direction. It does not change the approved Central/Branch deployment topology, business policies, database ownership, or API contracts.

## 2. Why this fits the system

The system contains meaningful business invariants in inventory, reservations, pricing, POS sales, payments, returns, loyalty and synchronization. These rules must not be embedded in API controllers, Angular clients, EF Core configurations or background-job plumbing.

Clean Architecture isolates business use cases from infrastructure. Pragmatic DDD gives each business module ownership of its rules and model without introducing a separate microservice or a separate deployable application per module.

## 3. Clean Architecture dependency rule

Dependencies point inward:

- **Domain** contains business concepts and invariants. It has no dependency on EF Core, ASP.NET Core, SQL Server, external providers or other outer layers.
- **Application** contains use cases, commands/queries where useful, authorization requirements, validation, ports/interfaces and transaction orchestration. It depends on Domain and approved contracts.
- **Infrastructure** implements persistence, external providers, messaging transports, file storage and other technical adapters. It depends inward on Application/Domain.
- **API / Worker hosts** are composition roots. They configure dependency injection, authentication, HTTP endpoints and background-worker hosting. They call Application use cases and do not contain business rules.

The dependency rule is about compile-time references; runtime control may flow through interfaces and dependency injection.

## 4. Pragmatic DDD rules

1. Organize the code by the approved business modules (for example Inventory, Sales, Payments, Pricing, Procurement, Online, CRM and Fulfillment), not by database table.
2. Each module owns its Domain model, Application use cases and persistence mapping for its data.
3. Use Aggregates only where a true consistency boundary exists; do not turn every table into an Aggregate Root.
4. Use Value Objects for concepts with identity-free meaning and validated value semantics (for example Money, Quantity, SKU or BranchCode where justified).
5. Put invariant enforcement in the Domain model or the owning Application use case, according to whether the rule is intrinsic to the model or requires orchestration.
6. Use Domain Events for meaningful in-process facts when they reduce coupling. Use explicit versioned Integration Events/contracts for cross-module or cross-site communication.
7. Keep cross-module interaction behind published Application contracts. No direct access to another module's EF entities, DbSets or tables.
8. Keep Central and Branch as separate data authorities. Cross-site communication remains API/command/Outbox/Inbox based; no shared database context or cross-database transaction.
9. Do not introduce Event Sourcing, CQRS infrastructure, a message broker, or a generic repository/unit-of-work framework by default. Adopt a pattern only for a documented use case and measurable benefit.

## 5. Proposed solution/project boundaries

The initial solution should preserve strong boundaries without creating one project per module or table.

### Shared technical and contract projects

- `Supermarket.BuildingBlocks`: minimal technical primitives only (result/error abstractions, clock/ID abstractions and similarly stable cross-cutting code). No business entities or module logic.
- `Supermarket.Contracts`: versioned API/integration/synchronization contracts. No EF Core entities and no references to Central/Branch implementation projects.

### Central application

- `Central.Domain`
- `Central.Application`
- `Central.Infrastructure`
- `Central.Api`
- `Central.Worker`

### Branch application

- `Branch.Domain`
- `Branch.Application`
- `Branch.Infrastructure`
- `Branch.Api`
- `Branch.Worker`

The Domain and Application projects are internally organized by business module. Infrastructure contains module-owned EF Core configurations and adapters. API and Worker are separate hosts and composition roots; they reuse the same application/infrastructure assemblies for their respective site.

This is a proposed initial project map. Exact namespaces, folders, package references and context registrations are finalized in the solution blueprint before project creation.

## 6. Persistence and EF Core boundaries

- Keep one Central persistence context and one Branch persistence context per physical database initially, as proposed by TD-26.
- Module-owned configurations map only the owning module's entities.
- DbContext and EF Core types remain in Infrastructure; Domain and Application do not reference EF Core.
- Application use cases define transaction boundaries. Infrastructure implements the transaction mechanism.
- Avoid exposing IQueryable or DbSet outside the owning persistence boundary.
- Avoid generic repositories that merely wrap EF Core. Add repositories only for Aggregate Roots or persistence behavior that provides a meaningful domain boundary.
- Migrations remain separate for Central and Branch databases, as required by TD-26.

## 7. CQRS and validation policy

- Use separate read and write models only where query needs or business consistency justify them.
- Use straightforward Application services/use cases for ordinary CRUD and workflows.
- Use explicit commands/queries for complex workflows such as stock reservation, invoice completion, refund processing and synchronization handling.
- Do not require MediatR or a mediator library. A library may be evaluated later if it materially improves the codebase.
- Validate input at API boundaries and enforce business invariants again in the owning Domain/Application layer. FluentValidation may be used for request/use-case validation but does not replace domain rules.

## 8. Testing requirements

- Domain unit tests cover value objects, aggregate invariants and state transitions without a database.
- Application tests cover use-case orchestration, authorization decisions and transaction intent.
- Infrastructure integration tests use SQL Server-compatible behavior for EF mappings, constraints, concurrency and migrations.
- Architecture tests enforce dependency direction, module boundaries and absence of forbidden references.
- End-to-end tests cover critical Central/Branch workflows and synchronization recovery.

## 9. Explicit non-goals

This decision does not introduce:
- Microservices or one deployable service per module.
- One project per database table.
- Event Sourcing.
- Mandatory full CQRS or a mediator framework.
- A universal repository for every entity.
- A shared Domain model between Central and Branch.
- Cross-database EF transactions or direct cross-site database access.

## 10. Approval boundary and next step

Approval of TD-27 authorizes the code-architecture blueprint only. It does not authorize creating entities, SQL tables, migrations or application features.

After approval, produce the exact Solution/project dependency map and module folder layout. Then create the empty .NET solution and verify its build before implementing the first vertical slice.

