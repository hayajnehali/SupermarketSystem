# BuildingBlocks and Central Host Wiring

## Shared abstractions

- `IClock` provides UTC time. Production uses the singleton `SystemClock`; tests can inject a fixed clock.
- `ICurrentActor` describes the authenticated user, employee, branch, and device.
- `IOperationContext` provides operation and correlation identifiers, plus the authenticated device identifier.

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
- `Central.Worker` registers the same application and infrastructure services for background processing.
- The Worker is a separate executable. It is not Hangfire itself; a background-job server can be hosted inside it when the durable job-processing slice is implemented.
