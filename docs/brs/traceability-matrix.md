# BRS Traceability Matrix — Decision to Requirement Mapping

**Version:** 0.1  
**Status:** Consolidated decision-family mapping; detailed test-case links are maintained with the relevant acceptance criteria and will be expanded during test planning.

| Approved source decision / family | Business topic | BRS requirement area |
|---|---|---|
| GOV-01–GOV-09 | Central governance, employee authority, customer-data access | BRS-ORG-001–029; CRM-CUS-007; CRM-CMP-004 |
| PUR-01–PUR-11; supplier purchasing decisions 32–34 | Supplier approval, purchase orders, receiving, invoice matching, supplier payment, supplier alerts | BRS-PUR-001–025; Unit 12 alert requirements where relevant |
| PRD-01–PRD-02; product decisions 59–63 | Product governance, SKU, barcode, categories, selling units, conversion, weighted and composite products | BRS-PRD-001–013 and Unit 02 selling-unit/composite-product requirements |
| INV-01–INV-22; STK-01–STK-02; EXP-01 | Batch cost, FIFO/FEFO, stocktake, transfers, expiry, reorder, stock increase | BRS-INV-001–049 |
| PRC-01–PRC-06; PRM-01–PRM-03; CUP-01–CUP-03 | Central prices, promotions, stacking, coupon conditions, offline pricing | PRC-01–PRC-12 |
| POS and invoice decisions; invoice numbering decision Q-F01 | Cashier shifts, POS sales, price restrictions, split payments, invoice sequence and reprints | POS-01–POS-20 |
| RET-01–RET-07; EXC-01; REF-01; DOC-01 | Supplier returns, branch purchase returns, website returns, exchanges, refunds, invoice copies | PMT-01–PMT-16 and related supplier-return requirements |
| WEB-01–WEB-19; ORD-01–ORD-02; ADR-01–ADR-04; DLV-01–DLV-04 | Online store, branch selection, stock reservation, payment, address, fees, minimum order, cancellation, delivery | WEB-001–WEB-026; DLV-001–DLV-019 |
| OFF-01–OFF-11; GAP-11–GAP-12; GAP-14 | Offline duration, local approved data, conflict isolation, safe operations, synchronization, reconciliation and resumption | OFF-001–OFF-022; POS-16 |
| CUS-01–CUS-04; LOY-01–LOY-04; CRM-01; P31–P35 | Customer registration, OTP, unique phone, duplicate-account resolution, loyalty, complaints | CRM-CUS-001–007; CRM-LOY-001–006; CRM-CMP-001–004 |
| FIN-01–FIN-10; FIN-05–FIN-07; P36–P37 | Expense request, central approval, proof of execution, report access and filters | OPS-EXP-001–005; OPS-RPT-001–004 |
| OPS-01–OPS-04; MNT-01; P38 | Maintenance requests, technician assignment, evidence, report, branch confirmation and central closure | OPS-MNT-001–004 |
| OPS-02; NTF-01–NTF-03; P39 | Central alert selection, channels, recipients, acknowledgement and history | OPS-ALT-001–004 |
| SEC-01–SEC-03; AUD-01 | Security monitoring, incident review, audit history | Business foundation shared rules; BRS-ORG-027–029; relevant payment, delivery, alert, and Offline audit requirements |
| BCP-01–BCP-02; BAK-01–BAK-03 | Local/central backups, offsite copy, central restore authority | Non-functional requirements NFR-BCP-001–004 |

## Identifier conventions

- `PMT-` is the prefix for payment, refund, and return requirements in Unit 07.
- `FIN-03` in the approved decision log governs the limited central payroll scope. Historical references to `PAY-01` for limited payroll are not Unit 07 payment requirement identifiers.
- The payment requirement prefix was standardized to `PMT-` to avoid the ambiguity.
- Recommendation groups P1–P40 remain approved owner recommendations; the decision-family codes above are the more precise traceability anchors wherever they are recorded in the discovery log.

## Traceability completion rule

A requirement is considered traceable when it has:
1. A stable BRS requirement identifier.
2. An approved source decision or approved recommendation.
3. A testable acceptance criterion in its BRS part.
4. A verification test identifier when the test plan is created.

This matrix maps approved decision families to BRS requirement areas. It is not a substitute for the later requirement-to-test-case matrix.
