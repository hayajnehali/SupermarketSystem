# Technical Design

**Status:** In progress — owner review required  
**Source of truth:** Merged BRS and approved discovery decisions.  
**Rule:** Technical documents may detail implementation choices but must not alter approved business requirements.

## Deliverables

| Document | Coverage | Status |
|---|---|---|
| [TD-01 — Architecture Overview](01-architecture-overview.md) | Distributed modular-monolith topology, Central/Branch APIs and Workers, Angular clients, data authority, synchronization principles | Initial baseline for review |
| [TD-02 — Recommended Technology Stack](02-recommended-technology-stack.md) | C#, .NET 10, ASP.NET Core, EF Core, SQL Server 2025, Angular 22, TypeScript 6, Node.js 26 | Recommendation pending owner approval |
| [TD-03 — Backend Modules and Dependencies](03-backend-modules-and-dependencies.md) | Business modules, Central/Branch deployment map, ownership, dependency and workflow boundaries | Proposal pending owner review |
| [TD-04 — Database Topology and Data Ownership](04-database-topology-and-data-ownership.md) | Central/branch databases, authoritative data ownership, transaction boundaries, stock/order/payment consistency | Proposal pending owner review |\n| [TD-05 — Central/Branch Synchronization](05-central-branch-synchronization.md) | Outbox/Inbox, idempotency, message flow, checkpoints, retries, conflicts, Offline resumption gate | Approved by owner |\n| [TD-06 — Identity, Authentication, Authorization, and Security](06-identity-authentication-authorization-security.md) | Employee/customer identities, Offline login, RBAC, branch isolation, POS devices, service authentication, data protection and audit | Approved by owner |\n| [TD-07 — API Boundaries and External Integrations](07-api-boundaries-and-external-integrations.md) | Branch/Central API ownership, critical workflows, payment/OTP integrations, webhooks, versioning and API security | Approved by owner |\n| [TD-08 — Deployment, Infrastructure, Backup, and Disaster Recovery](08-deployment-infrastructure-backup-disaster-recovery.md) | Central/branch deployment, networking, SQL operations, backups, RPO/RTO, recovery, release and monitoring | Proposal pending owner review |

## Next design sequence

1. Confirm architecture and technology baseline.
2. Review backend module boundaries and dependency rules.
3. Review database topology and data ownership.
4. Define synchronization contracts, idempotency, retries, conflict isolation, and resumption.
5. Define identity, authentication, authorization, and security controls.
6. Define API boundaries and external integrations.
7. Define deployment, backup, monitoring, and disaster recovery.
8. Review the complete technical design before implementation planning.

No application code should be generated until the relevant design baseline is approved.
