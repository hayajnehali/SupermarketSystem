# Technical Design — Backend Modules and Dependency Boundaries

**Document:** TD-03  
**Version:** 0.1  
**Status:** Initial module-boundary proposal — pending owner review  
**Depends on:** Merged BRS, TD-01 Architecture Overview.  
**Purpose:** Define business-aligned backend modules and their ownership without splitting the initial system into microservices.

## 1. Module design principles

1. A module owns its business rules, application use cases, and data model.
2. Modules are logical boundaries inside the Central or Branch Modular Monolith; they are not separate deployable microservices in the initial release.
3. A module must not read or write another module's tables directly.
4. Cross-module interaction uses explicit application contracts, commands/queries, or domain/integration events.
5. Cross-site interaction uses the approved synchronization boundary; neither side directly accesses the other site's database.
6. Central authority and branch-local operational ownership remain as defined in the BRS.
7. Shared technical utilities must not become a place for business rules or cross-module data access.
8. The module map is a logical responsibility map, not a final class/project/folder structure.

## 2. Proposed business modules

| Module | Main responsibilities | Primary authority / location |
|---|---|---|
| Organization & Access | Branch records, employees, user accounts, roles, permissions, device registration, branch context, sessions and access audit | Central owns configuration; Branch validates locally cached authorization within approved Offline limits |
| Product Catalog | Product identity, categories, SKU/barcodes, product attributes, selling units, conversions, composite-product definitions and product images | Central |
| Supplier & Procurement | Supplier records, supplier terms, branch purchase requests, purchase orders, purchase approvals, supplier invoices and supplier purchase returns | Central owns supplier/purchasing decisions; Branch records permitted receipt evidence |
| Inventory & Stock | Branch stock balances, batches, stock movements, costing, transfers, stocktakes, damaged/expired stock, reorder requests and availability | Each branch owns its physical stock ledger; Central authorizes controlled operations and consolidates visibility |
| Pricing & Promotions | Base prices, selling prices, promotion rules, coupons, validity, eligibility and price publication | Central; Branch consumes the latest approved local copy |
| POS & Sales | POS cart, sale completion, invoice numbering, cashier shifts, drawers, split tender allocation, sale snapshots and receipt reprints | Branch for local POS transaction execution; central receives synchronized records |
| Payments & Refunds | Payment attempts/results, tender records, provider references, refund decisions/execution, idempotency and payment reconciliation | Central owns online payment-provider orchestration; Branch records local POS tender outcomes and permitted refund operations under central rules |
| Online Store & Orders | Customer-facing catalog projection, cart/checkout, branch selection, order lifecycle, 15-minute reservation, online payment state and cancellation | Central |
| Fulfillment & Delivery | Picking, barcode verification, substitutions/unavailability handling, packing, pickup handover, delivery assignment, single delivery attempt, proof and dispute workflow | Central controls policy and assignment; assigned Branch executes operational steps |
| Offline & Synchronization | Offline policy distribution, connectivity state, outbox/inbox, operation IDs, checkpoints, retry, conflict isolation, reconciliation and central resumption authorization | Central governs policy and approval; each Branch owns its local synchronization agent and local message state |
| Customer, Loyalty & Complaints | Customer profile, phone verification, duplicate-phone controls, loyalty accounts/points, expiry, redemption, return adjustments and complaint resolution | Central |
| Expense & Maintenance | Expense requests, central approvals, execution evidence, maintenance requests, technician assignment, work reports and closure | Central owns approvals and closure; Branch submits requests and confirms local operational completion |
| Alerts & Reporting | Alert rules/threshold configuration, recipients, delivery, acknowledgement/history, central reports and authorized report projections | Central |

### Module placement notes

- Central and Branch hosts may contain different subsets of modules. A module name does not imply that all its capabilities are exposed in both hosts.
- The Inventory module is present in both contexts only as separate responsibilities: central authorization/visibility and branch-owned physical stock execution. It is not a shared database or shared stock ledger.
- POS & Sales runs in Branch API for local POS operations. Central receives the finalized sale history through synchronization; it does not execute the branch sale against the central database.
- Online Store & Orders runs centrally. Branch fulfillment commands are delivered through authenticated synchronization/application contracts.
- Payment-provider callbacks and online order payment state are handled centrally. A branch POS payment terminal integration is a separate adapter; it must not treat a browser redirect as payment confirmation.
- Shared customer identity and loyalty rules are centrally governed. Branch POS may use only the approved local customer/loyalty data required to complete permitted sales during the valid Offline period.

## 3. Central and Branch module deployment map

| Module | Central.Api / Central.Worker | Branch.Api / Branch.Worker |
|---|---|---|
| Organization & Access | Full administration and policy authority | Local authentication/authorization validation and branch/device context |
| Product Catalog | Full management and publication | Read-only approved local catalog projection |
| Supplier & Procurement | Full supplier and purchasing workflows | Receiving evidence and permitted branch-side actions |
| Inventory & Stock | Central approvals, consolidated views, transfer orchestration | Physical stock ledger and branch stock operations |
| Pricing & Promotions | Full price/promotion management and publication | Read-only approved price/promotion snapshot and local calculation |
| POS & Sales | Synchronized history and central reporting projections | Full local POS sale and invoice execution |
| Payments & Refunds | Online payment orchestration, provider callbacks, central refund coordination | Local POS tender capture/result and permitted branch refund execution |
| Online Store & Orders | Full order/checkout/reservation lifecycle | Assigned order work projection and fulfillment execution |
| Fulfillment & Delivery | Policy, assignment, monitoring, dispute decisions | Picking, packing, pickup/delivery handover and attempt evidence |
| Offline & Synchronization | Central policy, inbound/outbound coordination, conflict review and resume authorization | Local outbox/inbox, sync agent, retry, checkpoints and local status |
| Customer, Loyalty & Complaints | Full customer, loyalty and complaint authority | Restricted local lookup/use for approved POS and assigned workflows |
| Expense & Maintenance | Approval, assignment, review and closure | Request submission and local completion confirmation |
| Alerts & Reporting | Rule configuration, recipients, central delivery and reports | Local operational event production and authorized branch-scoped views only |

## 4. Module dependency rules

### 4.1 Allowed interaction

- A module may invoke another module through a published application contract.
- A module may consume an integration event published by another module.
- A module may use shared technical abstractions for clock, ID generation, current actor, result types, and message envelopes.
- Cross-module operations that must be atomic within one database transaction must be explicitly documented as a use case coordinated by the owning module; do not create hidden table-level coupling.

### 4.2 Prohibited interaction

- Direct SQL queries or writes against another module's tables.
- Sharing EF Core entities or persistence models across module boundaries.
- Circular module dependencies.
- UI clients bypassing APIs to access module databases.
- Central application directly connecting to branch SQL databases, or vice versa.
- A Branch module overriding centrally governed product, price, promotion, employee, permission, or policy data.
- A reporting module changing operational records as a side effect of reading them.

### 4.3 Shared kernel boundaries

A small shared kernel may contain only stable technical primitives, for example:
- Strongly typed identifiers and common result/error contracts.
- Clock abstraction and timestamp conventions.
- Current actor/device/branch context contracts.
- Message envelope metadata and correlation/causation identifiers.
- Common audit metadata contracts where they do not embed business decisions.

It must not contain product, pricing, inventory, payment, order, loyalty, or other business entities and rules.

## 5. Cross-module workflow ownership

| Workflow | Owning module | Collaborating modules |
|---|---|---|
| POS sale completion | POS & Sales | Product Catalog projection, Pricing & Promotions, Inventory & Stock, Payments & Refunds, Customer & Loyalty, Audit |
| Purchase receiving | Supplier & Procurement for purchase workflow; Inventory & Stock for stock ledger posting | Product Catalog, Inventory & Stock, Audit |
| Central price publication | Pricing & Promotions | Product Catalog, Offline & Synchronization |
| Online checkout | Online Store & Orders | Product Catalog, Pricing & Promotions, Inventory availability, Payments & Refunds, Customer |
| Online order picking | Fulfillment & Delivery | Online Store & Orders, Inventory & Stock, Product Catalog |
| Refund/return | Payments & Refunds for monetary outcome; Inventory & Stock for returned stock disposition | POS & Sales / Online Store & Orders, Customer & Loyalty, Audit |
| Inter-branch transfer | Inventory & Stock | Organization & Access, Offline & Synchronization |
| Offline resumption | Offline & Synchronization | All modules with pending operations; Central approval |
| Expense approval | Expense & Maintenance | Organization & Access, Alerts & Reporting, Audit |
| Maintenance completion | Expense & Maintenance | Organization & Access, Alerts & Reporting, Audit |

Where a workflow crosses more than one module, the owning module coordinates the use case and other modules expose explicit commands or events. The table does not prescribe a distributed transaction across sites.

## 6. Reporting boundary

- Operational modules remain the owners of their records.
- Central reporting reads authorized reporting projections or read models built from synchronized data.
- Branch reports are branch-scoped and do not grant branch staff access to central-only sales/profit/inventory reports.
- Report generation must not mutate sales, stock, payment, order, or approval records.
- The initial design may use relational read models; a separate analytics platform is not assumed.

## 7. Implementation structure guidance

The eventual solution may use separate projects or assemblies for strong compile-time boundaries, or a smaller project structure with explicit module folders and architecture tests. The final choice must preserve the dependency rules above.

A likely high-level grouping is:

- `Central`: API host, Worker host, module implementations, infrastructure adapters.
- `Branch`: API host, Worker host, module implementations, infrastructure adapters.
- `Contracts`: versioned integration/synchronization contracts only.
- `BuildingBlocks`: minimal technical primitives only.

Do not create one project per table or one microservice per module.

## 8. Decisions still required in detailed design

1. Exact module-to-project/assembly mapping.
2. Which modules share a database transaction in each approved workflow.
3. Exact branch/central data ownership matrix at entity and field level.
4. Integration event and synchronization message catalogue.
5. Read-model refresh and reporting projection strategy.
6. Authorization policy names and permission catalogue.
7. Exact API endpoint and DTO contracts.

These details are deferred to subsequent technical design documents; this module map does not reopen the approved business processes.

## 9. Approval boundary

TD-03 proposes logical module boundaries and dependency rules. It does not freeze source-code project names, database schemas, entity relationships, API contracts, or authorize implementation.
