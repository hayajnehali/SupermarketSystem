# BuildingBlocks and Central Host Wiring

## Shared abstractions

- `IClock` provides UTC time. Production uses the singleton `SystemClock`; tests can inject a fixed clock.
- `ICurrentActor` describes the authenticated user, employee, branch, and device.
- `IOperationContext` provides operation and correlation identifiers, plus the authenticated device identifier when available. The device ID comes from a trusted authenticated claim, never directly from an arbitrary request header.

The BuildingBlocks project contains cross-cutting contracts and small shared primitives. It does not contain Product, Order, Inventory, or other module domain models.

## Central HTTP adapters

Central Infrastructure supplies HTTP-aware adapters:

- `HttpCurrentActor` reads identity claims only when the principal is authenticated.
- `HttpOperationContext` reads GUID values from `X-Operation-Id` and `X-Correlation-Id`. If absent or invalid, it generates an operation ID and defaults the correlation ID to it.
- Device identity is read from the authenticated `device_id` claim, not trusted from an arbitrary request header.

## Audit safety

Product writes require an authenticated actor with a valid user ID. A background worker must not impersonate a user or silently write null audit identity. When system-initiated audited writes are introduced, they must use an explicitly configured service identity and be recorded as such.

## Hosting

- `Central.Api` registers the Central Application and Infrastructure services.
- `Central.Worker` registers the same application and infrastructure services for background processing. Its HTTP actor adapter has no authenticated principal in a normal background execution; system writes must use an explicit service-actor policy rather than impersonating a cashier.
- The Worker is a separate executable. It is not Hangfire itself; a background-job server can be hosted inside it when the durable job-processing slice is implemented.

## Application architecture decision

The solution uses a conventional Application Service Layer, not CQRS.

- Use cohesive services such as `ProductService`, `InventoryService`, and `SalesService`.
- Expose business use cases as service methods such as `CreateAsync`, `UpdateAsync`, and `DeactivateAsync`.
- Use request/DTO models as method inputs and response/DTO models as outputs where appropriate.
- Do not introduce CQRS Commands/Queries, separate Handlers, MediatR, or command/query buses.
- Keep business invariants in Domain entities and aggregates; keep orchestration in Application Services; keep EF Core and external integrations in Infrastructure.

This is an explicit project-wide architecture constraint unless the owner later changes it.

## Application and Infrastructure folder conventions

Use feature-first organization: each business feature owns its related application code, then separates that code by responsibility. This keeps a feature cohesive without creating large solution-wide folders containing unrelated feature files.

For example, Product Catalog follows this structure:

```text
Central.Application/
├── Abstractions/
│   └── ICentralUnitOfWork.cs
└── ProductCatalog/
    ├── Requests/
    │   ├── CreateProductRequest.cs
    │   └── CreateProductCategoryRequest.cs
    ├── Interfaces/
    │   ├── IProductRepository.cs
    │   └── IProductCategoryRepository.cs
    └── Services/
        ├── ProductService.cs
        └── ProductCategoryService.cs

Central.Infrastructure/
└── ProductCatalog/
    └── Repositories/
        └── ProductRepository.cs
```

- `Requests`: input DTOs for application service methods. Add `Responses` when a use case needs output DTOs; do not create empty or speculative response types.
- `Interfaces`: contracts consumed by Application Services, including feature repository interfaces.
- `Services`: use-case orchestration and application workflows.
- `Repositories`: concrete persistence implementations in Infrastructure, normally using EF Core.
- Shared application contracts such as `ICentralUnitOfWork` remain in `Application/Abstractions`; they are not duplicated inside every feature.

Apply the same feature-first convention to Central and Branch modules. Do not place a concrete EF Core repository in the Application project, and do not make a DbContext double as a feature repository. This is a folder organization convention, not CQRS; application use cases remain methods on cohesive services.
