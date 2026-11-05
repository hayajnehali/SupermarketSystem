# Technical Design

**Status:** In progress — owner review required  
**Source of truth:** Merged BRS and approved discovery decisions.  
**Rule:** Technical documents may detail implementation choices but must not alter approved business requirements.

## Deliverables

| Document | Coverage | Status |
|---|---|---|
| [TD-01 — Architecture Overview](01-architecture-overview.md) | Distributed modular-monolith topology, Central/Branch APIs and Workers, Angular clients, data authority, synchronization principles | Initial baseline for review |
| [TD-02 — Recommended Technology Stack](02-recommended-technology-stack.md) | C#, .NET 10, ASP.NET Core, EF Core, SQL Server 2025, Angular 22, TypeScript 6, Node.js 26 | Recommendation pending owner approval |

## Next design sequence

1. Confirm architecture and technology baseline.
2. Define backend modules and dependency boundaries.
3. Define database topology, data ownership, and branch/central data responsibilities.
4. Define synchronization contracts, idempotency, retries, conflict isolation, and resumption.
5. Define identity, authentication, authorization, and security controls.
6. Define API boundaries and external integrations.
7. Define deployment, backup, monitoring, and disaster recovery.
8. Review the complete technical design before implementation planning.

No application code should be generated until the relevant design baseline is approved.
