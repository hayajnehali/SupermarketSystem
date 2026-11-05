# BRS Comprehensive Review Report

**Review date:** 2026-11-05  
**Repository:** `hayajnehali/SupermarketSystem`  
**Review branch:** `docs/brs-comprehensive-review-final`  
**Status:** Findings addressed in this branch; owner review required before merge.

## 1. Review method

The review compared the five BRS parts against the approved decision log, meeting minutes, and closed operational GAP records. It used five analytical perspectives:

1. Business analysis — scope, roles, process completeness, and consistency.
2. Requirements verification — clarity, testability, exceptions, and traceability.
3. Implementation review — whether requirements express business outcomes without prematurely prescribing technology.
4. Audit/control review — approvals, segregation of duties, history, and accountability.
5. Customer/operational review — customer journeys, failed cases, and branch workflow.

These are review perspectives, not claims that external people participated.

## 2. Findings and corrections

| ID | Finding | Correction in this branch |
|---|---|---|
| REV-01 | Units 08–10 had been drafted on an earlier branch but were not present in `master`. | Added a complete Unit 08–10 requirements part with scenarios and acceptance criteria. |
| REV-02 | Unit 01–04 wording could imply branch staff execute physical inter-branch transfers and stocktaking, conflicting with the decision log. | Clarified that Central Administration employees execute inter-branch transfers and perform physical stocktaking; branches submit requests or cooperate within permitted tasks. |
| REV-03 | The Branch Manager role name could be mistaken for independent branch authority. | Clarified in the business foundation that the role grants no independent administrative or operational authority; only centrally granted permissions apply. |
| REV-04 | Unit 05–07 used `PAY-01` for payment requirements while `PAY-01` is also used in conversation history for limited payroll scope. | Renamed payment requirement identifiers to `PMT-01` onward; payroll remains under the approved FIN-03 limited scope. |
| REV-05 | Currency precision in the Unit 05–07 draft stated three decimal places for final JOD amounts, conflicting with GAP-03. | Clarified that quantities/internal calculations retain precision, while final JOD invoice amounts are rounded to two decimal places; the technical half-rounding rule remains for technical design. |
| REV-06 | Unit 08–10 required a clearer distinction between online-order pickup and branch-created pickup orders. | Clarified the 15-minute online reservation and preserved the separate branch-created pickup rules, including paid orders not collected. |
| REV-07 | Customer privacy, customer address capture, loyalty expiry, and centrally managed loyalty rules were not fully represented in Unit 11. | Added requirements for restricted customer access, no reusable saved-address book, central loyalty expiry rules, and loyalty redemption across approved channels. |
| REV-08 | Unit 12 described an expense too generically and did not fully capture the approved pre-approval/post-execution workflow. | Added request fields, quotation/pro-forma support, central approve/reject/return, no purchase/payment before approval, and post-execution actual cost/final proof review. |
| REV-09 | Maintenance requirements omitted approved evidence and completion steps. | Added device/location/fault/photo-or-video/urgency, central technician assignment, technician report/cost, branch operational confirmation, and central closure. |
| REV-10 | Internal alert channels and handling were underspecified. | Added in-system, email, and mobile push channels; centrally configured recipients; acknowledgement/action notes; and retained alert history. |
| REV-11 | Report access and period ownership were not explicit enough. | Restricted sales/profit/inventory reports to Central Administration and clarified central period selection and branch/period/product filtering. |
| REV-12 | Approved stocktake session details were missing from Unit 04. | Added centrally defined sessions, multiple central counters, recount on variance, movement freeze by scope, central cancellation, session history, and central closeout. |
| REV-13 | Backup, recovery, audit, security, historical integrity, and precision targets were scattered across decision records. | Added a cross-cutting non-functional requirements part and decision-family traceability matrix. |
| REV-14 | Delivery attempt count and customer dispute timing needed final owner decisions. | Fixed the delivery attempt count at exactly one and removed any fixed customer dispute submission window; disputes may be submitted at any time. |
| REV-15 | Offline duration needed explicit confirmation of its governance. | Confirmed that Central Administration sets the Offline operating duration; the configured limit applies uniformly across branches. |

## 3. Important consistency rules checked

- Central Administration remains the authority for employee administration, customer-file search, supplier and purchase decisions, prices/promotions, complaints, financial approvals, inventory adjustments, and Offline resumption.
- Branch employees do not independently create users, grant permissions, change prices, approve central operations, or resolve business synchronization conflicts.
- Inter-branch transfers are requested by branches, approved and physically executed by Central Administration employees; quantities are deducted at dispatch and added only on actual receipt.
- Physical stocktaking is performed by Central Administration employees using blind count, recount, investigation, and central closeout rules.
- Online orders require registered customers, one selected branch, stock availability, electronic payment, and a 15-minute reservation.
- A late payment does not reopen a cancelled online order or reserve stock again; the customer creates a separate new order if still interested.
- Online order notifications are website notifications and email only. Internal operational alerts use the separately approved central channels.
- Delivery confirmation is manual without OTP/electronic signature; customer identity is checked for branch pickup.
- Customer disputes preserve the original delivery confirmation and require Central Administration review; a dispute does not automatically trigger a refund.
- Offline expiry stops sales; restoration of connectivity alone does not resume them. Resumption requires complete synchronization, conflict resolution, reconciliation, refreshed central data, and explicit central approval.
- Historical transaction values remain immutable.

## 4. Open details that remain configurable or belong to technical design

These are not reopened business decisions. The following values or implementation details must be configured or specified before implementation:

1. Exact centrally configured Offline duration.
2. Exact customer delivery-dispute submission period.
3. Exact number of permitted delivery attempts.
4. Exact retention duration for paid branch-created pickup orders not collected by customers.
5. Supplier-specific return terms, verified by Central Administration when each return is considered.
6. Technical rounding tie-breaking rule for midpoint values, consistent with GAP-03 final JOD invoice rounding.
7. Technical synchronization protocol, conflict-resolution interface, and reconciliation evidence.
8. Detailed report formulas and definitions, without changing approved report access and filters.
9. Exact alert thresholds and recipient assignments for each centrally selected alert type.
10. Detailed OTP provider, delivery provider, and retry/rate-limit settings.

## 5. Approval boundary

- This report does not change or reopen any approved owner decision.
- It does not approve the technical architecture, database schema, APIs, or implementation.
- The consolidated BRS remains a review candidate until the owner accepts the pull request.
- Superseded or duplicate open pull requests must be closed only after the consolidated pull request is successfully created and verified.
