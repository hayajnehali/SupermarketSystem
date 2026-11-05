# Business Requirements Specification (BRS)
## Part 6 — Cross-Cutting Non-Functional and Operational Requirements

**Version:** 0.1  
**Status:** Drafted only from approved project decisions and documented targets.  
**Purpose:** Record cross-cutting controls that apply to all 12 functional units.

## 1. Auditability

| ID | Requirement | Priority |
|---|---|---|
| NFR-AUD-001 | Important operational, financial, administrative, security, approval, and synchronization actions shall be recorded with the responsible actor and timestamp. | Must |
| NFR-AUD-002 | Protected audit records shall remain available for authorized review and shall not be alterable or deletable by ordinary operational users. | Must |
| NFR-AUD-003 | Where an operation changes an important value or status, the system shall retain the relevant before/after values, decision, reason, and actor where applicable. | Must |

## 2. Backup and recovery

| ID | Requirement | Priority |
|---|---|---|
| NFR-BCP-001 | Each branch shall perform backups of its local data, and Central Administration shall perform backups of central data. | Must |
| NFR-BCP-002 | Backups shall be stored independently of the primary server, with an off-site copy retained for disaster scenarios. | Must |
| NFR-BCP-003 | Central Administration alone shall decide and supervise restoration of data after disaster or data loss, including branch data restoration. | Must |
| NFR-BCP-004 | The central recovery target shall be RPO 15 minutes and RTO 4 hours. | Must |

## 3. Security and access

| ID | Requirement | Priority |
|---|---|---|
| NFR-SEC-001 | Access to business operations and information shall follow centrally managed roles, branch assignments, and explicitly granted permissions. | Must |
| NFR-SEC-002 | The system shall monitor suspicious sign-in activity, including repeated failed attempts and sign-ins from unusual devices or locations, and alert Central Administration. | Must |
| NFR-SEC-003 | Central Administration alone shall review security incidents and record the review outcome and action. A suspicious event shall not automatically suspend an account unless a separately approved rule requires it. | Must |
| NFR-SEC-004 | Customer information access shall be restricted to Central Administration for general search and management; branch and delivery employees may see only information required for their assigned orders. | Must |

## 4. Historical integrity

| ID | Requirement | Priority |
|---|---|---|
| NFR-DAT-001 | Completed invoices and financial records shall preserve the values that applied when the transaction was completed. | Must |
| NFR-DAT-002 | Later changes to products, prices, promotions, or other master data shall not rewrite completed historical transactions. | Must |
| NFR-DAT-003 | Historical operational records linked to financial or inventory activity shall not be physically deleted through ordinary operational workflows. | Must |

## 5. Precision and monetary handling

| ID | Requirement | Priority |
|---|---|---|
| NFR-PRE-001 | Quantities shall support precision up to three decimal places where the configured unit permits it. | Must |
| NFR-PRE-002 | Internal calculations shall avoid premature rounding; reports and inventory records shall retain underlying precise values. | Must |
| NFR-PRE-003 | Final invoice amounts in Jordanian dinars shall be rounded to two decimal places. The technical midpoint/tie-breaking rule shall be fixed in technical design before implementation. | Must |

## 6. Operational limits not numerically fixed here

The following values remain centrally configurable or require technical specification; this document does not invent a numeric value:

- Offline operating duration.
- Customer delivery-dispute submission period.
- Permitted delivery-attempt count.
- Retention period for paid branch-created pickup orders that remain uncollected.
- Backup retention schedule and backup frequency needed to meet the approved recovery targets.
- Detailed restore validation and disaster-recovery exercise cadence.

## 7. Boundaries

This part records approved operational targets and governance rules. It does not select a hosting provider, database, backup product, encryption algorithm, network protocol, or implementation design. Those belong to the technical architecture and security design phases.
