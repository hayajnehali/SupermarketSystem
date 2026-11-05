# Business Requirements Specification (BRS) — Master Index

**Project:** SupermarketSystem  
**Owner:** Project owner  
**Status:** Consolidated review candidate — pending owner approval  
**Last reviewed:** 2026-11-05

## 1. Authoritative source documents

The BRS shall be interpreted together with the approved discovery records. Where wording conflicts, the latest explicit owner-approved decision takes precedence over an older document or a derived requirement.

- [Project charter and discovery baseline](../discovery/project-charter.md)
- [Approved decision log](../discovery/decision-log-01.md)
- [Meeting minutes](../discovery/meeting-minutes-01.md)
- [Open questions and operational details](../discovery/open-questions-01.md)

## 2. BRS parts

| Part | Document | Coverage | Repository status |
|---|---|---|---|
| 1 | [Business foundation](01-business-foundation.md) | Purpose, scope, business objectives, governance, roles, shared rules | Existing; revised in this review branch |
| 2 | [Functional requirements — Units 01–04](02-functional-requirements-units-01-04.md) | Administration; products; suppliers/purchasing; inventory/stocktaking | Existing; revised in this review branch |
| 3 | [Functional requirements — Units 05–07](03-functional-requirements-units-05-07.md) | Pricing/promotions; POS/sales/invoices; payments/refunds/returns | Consolidated corrected copy; replaces superseded open PR #7 if this review is accepted |
| 4 | [Functional requirements — Units 08–10](04-functional-requirements-units-08-10.md) | Online store/orders; preparation/delivery; Offline/synchronization | Added in this review branch |
| 5 | [Functional requirements — Units 11–12](05-functional-requirements-units-11-12.md) | Customers/loyalty/complaints; expenses/maintenance/alerts/reports | Existing; revised in this review branch |

## 3. Unit map

1. Central, branch, employee, and user administration.
2. Products, categories, selling units, and barcodes.
3. Suppliers, purchasing, and receiving.
4. Inventory, batches, costing, and stocktaking.
5. Pricing, promotions, and discounts.
6. POS, sales, invoices, and cashier shifts.
7. Payments, refunds, returns, and exchanges.
8. Online store and customer orders.
9. Order preparation, pickup, and delivery.
10. Offline operation and synchronization.
11. Customers, loyalty, and complaints.
12. Expenses, maintenance, alerts, and reports.

## 4. Approved scope boundaries

- Multi-branch private supermarket chain.
- Centralized business governance; branch employees execute only explicitly permitted tasks.
- No wholesale, credit sales, customer debt management, or complete accounting/general-ledger scope in the current release.
- Electronic payment is in scope for online orders; online orders use a 15-minute stock reservation/payment window.
- Tax and e-invoicing are future-readiness placeholders only.
- Online orders are fulfilled by one branch and are never split across branches.
- The customer cannot edit order contents or branch after order confirmation/reservation.
- Offline sales stop when the centrally configured Offline limit expires; internet restoration alone does not resume sales.
- Historical invoices and financial transactions are not rewritten by later master-data changes.

## 5. Document control

This index is a navigation and status document. It does not supersede the detailed source decision log, and it does not by itself approve the consolidated BRS. Approval is complete only when the project owner reviews and accepts the consolidated review pull request.
