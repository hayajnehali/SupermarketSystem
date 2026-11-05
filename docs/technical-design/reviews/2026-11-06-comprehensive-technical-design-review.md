# Comprehensive Technical Design Review

**Date:** 2026-11-06  
**Repository:** hayajnehali/SupermarketSystem  
**Review branch:** docs/technical-architecture-baseline  
**Scope:** TD-01 through TD-08, checked against the BRS currently present on master and the owner's latest recorded decisions.  
**Review type:** Documentation and architecture consistency review; no application code or database schema has been implemented.

## 1. Executive outcome

The eight technical design documents form a coherent high-level architecture for the approved 3–5 branch supermarket system:

- Distributed Modular Monolith, not premature microservices.
- Central and per-branch API/Worker hosts and isolated databases.
- Angular POS, Store and Admin clients communicating through APIs.
- Branch-local POS operation within centrally governed Offline limits.
- Durable Outbox/Inbox synchronization and idempotent business operations.
- Central governance of master data, pricing, policies, online orders and payment-provider workflows.
- Branch ownership of physical stock and locally executed POS transactions.

The review found no reason to replace the selected high-level architecture. However, the design is **not yet ready to freeze for implementation**. The key unresolved design issue is the atomic online stock-reservation handshake between Central and the selected Branch. There are also approval-state, detailed API, security, infrastructure and recovery details to close before coding.

## 2. Owner decisions preserved

The review preserves, and does not reopen, these decisions:

1. Central configures the maximum Offline duration uniformly for all branches.
2. Local employee login remains valid for no more than 24 hours after the last successful Central verification.
3. When the Offline limit expires, new sales and invoice issuance stop.
4. Restoring connectivity alone does not resume sales; required synchronization, reconciliation, refresh and explicit Central authorization are required.
5. A branch that is Offline/unavailable is not eligible for new website orders.
6. The online order reservation/payment window is 15 minutes, with payment confirmation verified by the backend/provider callback.
7. Exactly one customer delivery attempt is permitted.
8. A technical synchronization retry is not a second delivery attempt.
9. No separate customer delivery-dispute deadline/window is introduced. Customers may dispute at any time; Central Administration decides and records the reason. A dispute alone does not automatically cause a refund.
10. Central recovery targets remain RPO 15 minutes and RTO 4 hours. No branch RPO/RTO is inferred.
11. Existing business scope exclusions and approved roles, pricing, inventory, returns, customer and loyalty rules remain unchanged.

## 3. Findings and required actions

### TD-REV-01 — Online reservation handshake is not yet fully specified

**Severity:** High — must resolve before checkout implementation.

TD-04 and TD-05 intentionally defer the exact reservation handshake, branch acknowledgement timeout and stock freshness threshold. TD-07 describes the checkout flow, but does not yet establish the atomic point at which a website reservation becomes protected against a concurrent POS sale.

**Risk:** Central may accept payment while the selected branch has not yet durably reserved the stock locally. A POS sale could consume the same units before the branch applies the website reservation.

**Required design outcome:**

- Central creates a pending reservation command with a stable reservation/operation identifier.
- The selected branch must atomically validate available, sellable, unreserved stock and persist the reservation in its local database before acknowledging acceptance.
- POS sale validation and website reservation processing must serialize against the same branch stock/reservation state.
- Central must not treat stock as confirmed for checkout/payment until the branch's durable acceptance is recorded.
- The 15-minute business reservation/payment window must retain its approved meaning and must not silently start earlier or be extended by technical retries.
- A technical pre-acceptance timeout/failure must not create a confirmed order or trigger a duplicate charge. The exact timeout and customer-facing outcome must be documented without changing the approved 15-minute rule.
- Release/cancellation must be idempotent and acknowledged by the branch. The POS may sell released stock only after the branch has durably applied the release.
- If the branch becomes unavailable during the handshake, the checkout must fail safely rather than assume stock is reserved.

**Open technical choice:** Define the transport/coordination mechanism that allows Central to obtain a durable Branch acknowledgement while Branch connectivity is outbound-initiated (for example, bounded worker polling/long polling or an approved persistent channel). Do not use direct Central-to-Branch database access.

### TD-REV-02 — Technology baseline is a recommendation, not yet owner-approved

**Severity:** High — approval required before repository scaffolding.

TD-02 recommends C# 14, .NET 10 LTS, ASP.NET Core 10, EF Core 10, SQL Server 2025, Angular 22, TypeScript 6 and Node.js 26 LTS. The document itself is explicitly marked “Recommendation for owner approval.”

**Required action:** Owner must approve or change this baseline. Exact servicing patches and lockfile versions should be pinned at implementation start after checking official compatibility matrices. SQL Server edition/licensing remains a separate decision for Central and branches.

### TD-REV-03 — TD-01 through TD-04 remain proposals

**Severity:** High — approval-state gap.

TD-01 architecture overview, TD-02 technology stack, TD-03 module boundaries and TD-04 database ownership still carry proposal/pending approval statuses. The owner's later approvals of TD-05, TD-06 and TD-07 do not automatically approve TD-01 through TD-04.

**Required action:** Review and approve the foundational architecture, stack, module boundaries and data ownership as a single coherent baseline. Keep TD-08 pending until its deployment/recovery details are accepted.

### TD-REV-04 — API endpoint and state-transition catalogue is still deferred

**Severity:** Medium — required before implementation of integrations.

TD-07 defines logical API groups and workflows but defers exact routes, DTOs, status codes, error schemas, idempotency header/body convention, pagination, API contract generation and version compatibility window.

**Required action:** Produce an API contract specification for the critical workflows first: checkout/reservation/payment, POS sale, returns/refunds, delivery attempt/dispute, branch synchronization and Offline resumption. Ensure each command defines its durable success point, idempotency behavior, authorization scope and failure outcomes.

### TD-REV-05 — Security implementation details remain open

**Severity:** Medium — must close before authentication/security implementation.

TD-06 correctly avoids prematurely choosing products, but the following require a concrete implementation decision and threat review: local Offline credential verifier, employee/customer authentication/session strategy, privileged MFA, POS device provisioning/rotation, branch workload identity, signing-key management, token revocation behavior and the detailed permission catalogue.

**Required action:** Produce a threat model and security implementation decision record. In particular, ensure the local Offline verifier does not copy reusable plaintext credentials or long-lived Central secrets to branch databases.

### TD-REV-06 — Backup/RPO controls need operational proof

**Severity:** Medium — must prove before production.

TD-08 now clarifies that Central's 15-minute RPO requires a backup/recovery configuration that includes sufficiently frequent transaction-log backups under the selected SQL Server recovery model; full/differential backups alone do not establish the RPO.

**Required action:** Select the Central SQL recovery model, log-backup interval and protected backup destination, then prove the 15-minute RPO and 4-hour RTO with timed point-in-time restore exercises. Branch recovery targets remain open and must be decided separately before branch production rollout.

### TD-REV-07 — Deployment and runtime topology are logical, not sized

**Severity:** Medium — procurement/production-readiness task.

Hosting provider/region, Central and branch compute/storage sizing, SQL editions/licensing, branch hardware, network/ISP, monitoring platform, retention and capacity/performance targets remain deferred.

**Required action:** Complete a workload/capacity and cost assessment for the initial 3–5 branches before procurement. Do not interpret the logical diagrams as approved hardware specifications.

### TD-REV-08 — Customer delivery dispute workflow needed explicit API representation

**Severity:** Addressed in this review branch.

TD-07 now explicitly includes a delivery-disputes API group and workflow: customers may dispute at any time; Central Administration records accept/reject and reason; a dispute alone does not automatically trigger refund or another delivery attempt. This reflects the approved BRS without adding a dispute window.

## 4. Cross-document consistency checks

| Area | Review result |
|---|---|
| Central/Branch architecture | Consistent across TD-01, TD-03, TD-04, TD-07 and TD-08 |
| Modular Monolith boundary | Consistent; module names are logical boundaries, not microservices |
| Angular clients/API-only access | Consistent; no direct SQL access from clients |
| Branch stock ownership | Consistent; physical stock is branch-owned, Central holds synchronized projections |
| Offline policy and login | Consistent: central-configured Offline duration and separate 24-hour local login validity |
| Resume after connectivity restoration | Consistent: synchronization/reconciliation and explicit Central authorization required |
| One delivery attempt | Consistent; technical message retry is explicitly separate |
| Delivery dispute deadline | No separate deadline; TD-07 now states this explicitly |
| Online reservation/payment | 15-minute rule is preserved; atomic branch reservation handshake remains unresolved |
| Payment confirmation/refunds | Central provider callback verification and idempotency are consistently required |
| Central recovery | RPO 15 minutes / RTO 4 hours preserved; operational proof remains required |
| Branch recovery targets | Correctly left unspecified pending separate owner decision |

## 5. Approval and readiness status

| Document | Current status | Review disposition |
|---|---|---|
| TD-01 Architecture overview | Proposal pending owner review | Approve after checking this review's findings |
| TD-02 Technology stack | Recommendation pending owner approval | Requires explicit owner approval |
| TD-03 Backend modules | Proposal pending owner review | Approve as logical module baseline; detailed contracts remain later work |
| TD-04 Database topology/ownership | Proposal pending owner review | Approve ownership model; reservation handshake must be completed |
| TD-05 Central/Branch synchronization | Owner-approved | Retain approval; detailed reservation contract still required |
| TD-06 Identity/security | Owner-approved | Retain approval; implementation details remain deferred |
| TD-07 API/integrations | Owner-approved, updated with dispute workflow | Retain approval; detailed endpoint contracts remain deferred |
| TD-08 Deployment/DR | Proposal pending owner review | Review and approve after operational recovery requirements are understood |

## 6. Recommended next sequence

1. Owner approval of TD-01 through TD-04 and the TD-02 technology baseline.
2. Complete the online reservation handshake and stock-concurrency contract (TD-04/TD-05/TD-07).
3. Produce the critical API contract catalogue.
4. Complete security implementation threat model and identity/device decisions.
5. Complete Central/Branch infrastructure sizing, backup policy and tested recovery plan.
6. Run architecture and requirements traceability review.
7. Only then prepare implementation scaffolding and phased coding tasks.

## 7. Review boundary

This report identifies design gaps and required technical decisions. It does not alter approved business requirements, choose an unapproved technology/provider, invent operational time limits, set branch RPO/RTO, or authorize application coding.
