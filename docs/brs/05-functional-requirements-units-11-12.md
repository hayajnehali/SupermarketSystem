# Business Requirements Specification (BRS)
## Part 5 — Functional Requirements: Units 11–12

**Document version:** 0.5  
**Status:** Prepared from owner-approved recommendations P31–P40 for review  
**Parent document:** [01-business-foundation.md](01-business-foundation.md)  
**Scope:** Unit 11 — Customers, Loyalty, and Complaints; Unit 12 — Expenses, Maintenance, Alerts, and Reports.

This part documents the owner-approved recommendations P31–P40. It does not reopen or alter those decisions. It defines business-facing requirements and acceptance criteria without prescribing database structures, APIs, or implementation technology.

---

# Unit 11 — Customers, Loyalty, and Complaints

## 11.1 Functional requirements

| ID | Functional requirement | Priority |
|---|---|---|
| CRM-CUS-001 | The system shall verify ownership of a customer's mobile phone number using OTP during registration. | Must |
| CRM-CUS-002 | A customer account shall not become active until the phone OTP verification succeeds. | Must |
| CRM-CUS-003 | The mobile phone number shall be a unique customer identifier. The system shall prevent creating a second customer account using a phone number already associated with an existing account. | Must |
| CRM-CUS-004 | The system shall support an administrative process for handling duplicate customer accounts that already exist, without silently creating further duplicates. | Must |
| CRM-LOY-001 | Loyalty points earned from a sale shall be calculated and credited only after the sale is completed and its payment is confirmed. | Must |
| CRM-LOY-002 | When a sale is partially or fully returned, the system shall reverse the points earned for the returned products in proportion to their returned value, according to the approved loyalty rules. | Must |
| CRM-LOY-003 | When a return relates to a purchase for which loyalty points were already redeemed, the system shall handle the redeemed points according to the approved refund and return policy. | Must |
| CRM-CMP-001 | The system shall allow customer complaints to be recorded and assigned a unique reference number. | Must |
| CRM-CMP-002 | Each complaint shall have a status, an assigned responsible employee, and a record of actions taken. | Must |
| CRM-CMP-003 | The system shall preserve the complaint's history, including status changes and recorded actions, until the complaint is closed. | Must |
| CRM-CMP-004 | Complaint management and follow-up shall be controlled by Central Administration. | Must |

## 11.2 Main scenarios

### Scenario A — Customer registration and phone verification

1. A customer submits registration details and a mobile phone number.
2. The system sends an OTP to the submitted number.
3. The customer enters the OTP.
4. The system verifies the OTP.
5. Only after successful verification does the account become active.
6. If the phone number is already associated with an existing account, the system prevents creation of another account with that number.

### Scenario B — Earning loyalty points

1. A customer completes a sale.
2. The system waits for payment confirmation.
3. After the sale is completed and payment is confirmed, the system calculates and credits the earned points.
4. An unpaid, pending, or incomplete sale does not earn credited points.

### Scenario C — Return affecting loyalty points

1. A customer returns some or all products from a sale.
2. The system identifies the original sale and the returned products.
3. The system reverses the earned points attributable to the returned products according to their returned value.
4. If points were redeemed in the original purchase, the system handles them under the approved refund and return policy.
5. The loyalty transaction history remains traceable.

### Scenario D — Record and resolve a complaint

1. An authorized central user records a customer complaint.
2. The system assigns a unique reference number and an initial status.
3. A responsible employee is assigned.
4. The employee records actions and updates the complaint status.
5. Central Administration follows the complaint until it is closed.
6. The complaint's reference, responsible employee, status, and action history remain available.

## 11.3 Acceptance criteria

- A new customer account cannot become active before successful OTP verification of the mobile number.
- A phone number already linked to an existing customer account cannot be used to create another account.
- Loyalty points are not credited for incomplete sales or unconfirmed payments.
- Points earned on returned products are reversed according to the returned products' value and approved loyalty rules.
- Redeemed points affected by a return are handled according to the approved refund and return policy.
- Every complaint has a unique reference number, status, assigned responsible employee, and action history.
- Complaint status changes and actions remain traceable through closure.
- Complaint management remains under Central Administration.

---

# Unit 12 — Expenses, Maintenance, Alerts, and Reports

## 12.1 Functional requirements

| ID | Functional requirement | Priority |
|---|---|---|
| OPS-EXP-001 | The branch shall be able to record an expense and submit it to Central Administration for review. | Must |
| OPS-EXP-002 | A branch-recorded expense shall not be treated as financially approved until Central Administration approves it. | Must |
| OPS-EXP-003 | The system shall require an invoice or supporting document for an expense. An exception may be accepted only when Central Administration approves it and the exception is recorded. | Must |
| OPS-MNT-001 | The branch shall be able to submit a maintenance request identifying the device or asset, its location, and the reported fault. | Must |
| OPS-MNT-002 | Each maintenance request shall have a unique reference number, priority, assigned responsible person, recorded cost, status, and chronological history. | Must |
| OPS-MNT-003 | The system shall preserve maintenance request status changes and actions in a chronological record. | Must |
| OPS-ALT-001 | The system shall provide a centralized alerts center. | Must |
| OPS-ALT-002 | Alerts shall be classified by importance and shall identify the responsible person or role for follow-up. | Must |
| OPS-RPT-001 | The system shall provide centralized reports for management. | Must |
| OPS-RPT-002 | The system shall support comparison of branch performance and results. | Must |
| OPS-RPT-003 | Central reports and branch comparisons shall support filtering by branch, period, and product. | Must |

## 12.2 Main scenarios

### Scenario A — Submit and approve a branch expense

1. A branch employee records an expense.
2. The employee attaches an invoice or supporting document.
3. The branch submits the expense to Central Administration.
4. Central Administration reviews and approves or rejects it.
5. The expense is not treated as financially approved before central approval.
6. If supporting documentation is unavailable, the exception must be explicitly approved by Central Administration and recorded.

### Scenario B — Submit a maintenance request

1. A branch employee selects the affected device or asset.
2. The employee records its location and describes the fault.
3. The system creates a maintenance request with a unique reference number.
4. The request is assigned a priority and a responsible person.
5. Its cost and status are recorded as the request progresses.
6. The system preserves a chronological history of the request and its actions.

### Scenario C — Review centralized alerts

1. The system generates an operational alert.
2. The alert appears in the centralized alerts center.
3. The alert is classified by importance.
4. The responsible person or role is identified for follow-up.
5. The alert remains available for tracking according to its status.

### Scenario D — Review branch reports

1. Central Administration opens the reporting area.
2. The user selects a branch, reporting period, and/or product filter.
3. The system presents the corresponding report results.
4. Central Administration compares branch results using consistent report criteria.

## 12.3 Acceptance criteria

- A branch can record and submit an expense, but cannot mark it financially approved without Central Administration approval.
- An expense requires an invoice or supporting document unless Central Administration approves and records an exception.
- A maintenance request has a unique reference number, priority, responsible person, cost, status, and chronological history.
- Maintenance actions and status changes are retained in chronological order.
- Alerts are available through one centralized center, classified by importance, and identify who is responsible for follow-up.
- Centralized reports support branch comparison and filtering by branch, period, and product.

---

## 12.4 Traceability and boundaries

- P31: OTP verification of the mobile number at registration; the account is not activated before successful verification.
- P32: loyalty points are credited only after sale completion and payment confirmation.
- P33: earned points attributable to returned products are reversed according to the returned value; redeemed points are handled under the approved refund policy.
- P34: the mobile number is unique and cannot be used to create a second customer account.
- P35: complaints have a reference number, status, responsible employee, and action history.
- P36: branch expenses are recorded and submitted; Central Administration provides financial approval.
- P37: an invoice/supporting document is required, with a documented exception only when Central Administration approves it.
- P38: maintenance requests have a reference number, priority, responsible person, cost, status, and chronological history. The branch records the device, its location, and the fault.
- P39: a centralized alerts center classifies alerts by importance and identifies the responsible person.
- P40: centralized reports compare branches and can be filtered by branch, period, and product.
- Limited payroll tracking remains governed by the separately approved PAY-01 scope; this part does not introduce payroll calculation or statutory payroll processing.
- Financial approvals, audit, access control, and historical-record retention remain subject to the shared business rules in the approved BRS foundation.
- Detailed interfaces, data schemas, APIs, alert delivery channels, report calculations, and technical scheduling belong to technical design and are not newly specified here.
