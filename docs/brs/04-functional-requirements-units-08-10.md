# Business Requirements Specification (BRS)
## Part 4 — Functional Requirements: Units 08–10

**Document version:** 0.4  
**Status:** Prepared for owner review; aligned with approved decision log and closed operational GAPs  
**Scope:** Unit 08 — Online Store and Customer Orders; Unit 09 — Order Preparation, Pickup and Delivery; Unit 10 — Offline Operation and Synchronization.

This part defines business behavior and acceptance criteria. It does not prescribe APIs, database schema, or technical implementation.

---

# Unit 08 — Online Store and Customer Orders

## 8.1 Purpose

Enable customers to place and pay for online orders while enforcing centrally managed prices, branch availability, stock reservation, customer privacy, and order cancellation/refund policies.

## 8.2 Functional requirements

| ID | Functional requirement | Priority |
|---|---|---|
| WEB-001 | The online store shall require a registered customer account; guest checkout is not supported. | Must |
| WEB-002 | Customers shall be able to create an account, update personal information, and view their previous orders. | Must |
| WEB-003 | The customer shall select the branch responsible for fulfilling the order. A single online order shall be fulfilled by one branch only and shall not be split across branches. | Must |
| WEB-004 | The store shall not accept new orders for a branch that is offline or unavailable for online fulfillment. | Must |
| WEB-005 | The store shall not confirm an order unless all requested products and quantities are available at the selected branch. | Must |
| WEB-006 | The store shall display unavailable products with a clear unavailable status; unavailable products cannot be purchased. | Must |
| WEB-007 | Customers shall be able to search products by name and barcode and filter results by category, price, and brand. | Must |
| WEB-008 | Products shall be organized into main and subcategories. Product details shall display available approved information, including name, images, price, brand, country of origin, optional measurements, optional description, and relevant additional information when available. | Must |
| WEB-009 | A product may have multiple images. Only Central Administration may add or manage product images. | Must |
| WEB-010 | The customer shall select a city and a related village/area from configured lists and enter delivery-address details in free text for each order. The system shall not provide a reusable saved-address book. | Must |
| WEB-011 | The customer shall not select a delivery date or time window; delivery is handled according to the operational workflow. | Must |
| WEB-012 | Central Administration shall manage delivery fees by city/area and may define free-delivery conditions. | Must |
| WEB-013 | Central Administration shall define a minimum online order value that applies uniformly across delivery areas. The system shall prevent confirmation below that value. | Must |
| WEB-014 | Before the order is confirmed and stock is reserved, the customer may change cart contents and the selected branch. The system shall revalidate availability and recalculate applicable prices, promotions, delivery fees, and total after each relevant change. | Must |
| WEB-015 | Once the order is confirmed and the 15-minute stock reservation/payment window begins, the customer shall not be able to change order contents or the selected branch. | Must |
| WEB-016 | Online orders shall be paid electronically. A payment is successful only after trusted confirmation from the payment provider; pending or unverified responses shall not confirm the order. | Must |
| WEB-017 | Upon confirmation of an online order, the system shall reserve its required stock for 15 minutes while payment is pending. | Must |
| WEB-018 | If payment is not confirmed within 15 minutes, the system shall cancel the order and release its stock reservation. If payment is confirmed within the reservation period, the order proceeds to fulfillment and is not cancelled merely because the reservation period later elapses. | Must |
| WEB-019 | A late payment confirmation for an already cancelled order shall be recorded against the original order. It shall not restore the order, reserve stock again, or start preparation. The default handling is refund and notification to Central Administration for follow-up. | Must |
| WEB-020 | The Central Administration shall not recreate a cancelled order on behalf of the customer. The customer may place a separate new order using current prices and availability. | Must |
| WEB-021 | The customer shall not cancel a confirmed online order directly. A cancellation requested before preparation starts requires Central Administration approval. | Must |
| WEB-022 | Cancellation after preparation has started requires Central Administration approval, a recorded reason, and a recorded decision about the products. | Must |
| WEB-023 | If a product becomes unavailable during preparation, the branch shall stop preparation of the affected order and refer the case to Central Administration. Neither the branch nor the system shall propose or apply a substitute automatically. | Must |
| WEB-024 | Refunds for cancelled online orders shall be made to the original payment method where possible, after cancellation approval and verification of the order/payment state. Refund status shall be tracked independently from order status. | Must |
| WEB-025 | Customer order-status notifications shall use website notifications and email only. SMS shall not be used for these customer notifications. The system shall retain notification records and delivery results; a notification failure shall not change order or payment status. | Must |
| WEB-026 | Branch and delivery employees shall not search or browse customer profiles. They may view only the customer information needed for an order assigned to them; delivery employees may view only orders assigned to them. | Must |

## 8.3 Main scenarios

### Scenario A — Checkout and payment

1. A registered customer selects an available branch and adds available products to the cart.
2. The customer may change the cart or branch before confirming the order.
3. The system validates the selected branch, all requested quantities, current prices/promotions, delivery fee, and minimum order value.
4. The system presents the final order total.
5. On confirmation, the system reserves stock for 15 minutes and the customer completes electronic payment.
6. The order is confirmed for fulfillment only after trusted payment confirmation within the reservation period.

### Scenario B — Payment is not confirmed within 15 minutes

1. The reservation expires without trusted payment confirmation.
2. The system cancels the order and releases reserved stock.
3. A later provider confirmation is recorded as a late payment against the cancelled order.
4. The system does not restore the order, reserve stock, or start preparation.
5. The default handling is refund and Central Administration notification.
6. If the customer still wants the products, the customer creates a separate new order.

### Scenario C — Product becomes unavailable during preparation

1. The branch identifies that an ordered product cannot be fulfilled.
2. The branch stops preparation of the affected order and records the unavailable item.
3. Central Administration receives the case and decides the next action.
4. No substitute is proposed or applied automatically.

## 8.4 Acceptance criteria

- Guest checkout is unavailable.
- A single online order is assigned to one branch and cannot be split.
- A new order cannot be placed with an offline branch or confirmed if any requested quantity is unavailable.
- Unavailable products are visible but cannot be purchased.
- Search supports name and barcode; filters include category, price, and brand.
- The customer selects city and related area and enters the address for each order; no reusable address book is provided.
- Delivery fees are centrally managed by area; minimum order value is centrally defined and uniform across areas.
- Cart contents and branch can be changed only before order confirmation and reservation.
- A 15-minute reservation expires and is released when payment is not confirmed.
- A late payment does not reopen a cancelled order or reserve stock again.
- Customer notifications use website notifications and email only; failed notification delivery does not change order/payment state.
- Refunds are tracked independently and are marked successful only after provider confirmation.

---

# Unit 09 — Order Preparation, Pickup and Delivery

## 9.1 Purpose

Control preparation, item verification, assignment, delivery/pickup completion, failed delivery, returns to branch, and customer disputes with clear central oversight and traceable employee actions.

## 9.2 Functional requirements

| ID | Functional requirement | Priority |
|---|---|---|
| DLV-001 | The branch shall prepare only confirmed online orders assigned to that branch. | Must |
| DLV-002 | The system shall record preparation progress and order fulfillment status. | Must |
| DLV-003 | During picking, the employee shall scan each picked product barcode to verify it matches the ordered product. | Must |
| DLV-004 | The system shall reject a barcode mismatch and prevent the mismatched item from being recorded as the requested product. | Must |
| DLV-005 | The system shall preserve the ordered item and the actual picked item details so preparation discrepancies can be reviewed. | Must |
| DLV-006 | If an ordered product is unavailable, the branch shall stop preparation of the affected order and refer the case to Central Administration without proposing a substitute. | Must |
| DLV-007 | The system shall record the employee responsible for preparation and the relevant preparation status changes. | Must |
| DLV-008 | Central Administration shall appoint a delivery-task coordinator for each shift. The coordinator may be changed for a later shift. | Must |
| DLV-009 | The shift coordinator shall distribute delivery tasks and follow up their completion without independent administrative authority or permissions to change prices, inventory, employee permissions, or policies. | Must |
| DLV-010 | Each delivery task shall record the assigned delivery employee, assigning coordinator, assignment time, status changes, and relevant timestamps. | Must |
| DLV-011 | The delivery employee shall confirm delivery manually in the system. Delivery confirmation shall not require OTP or electronic signature. The system shall record the employee identity and confirmation time. | Must |
| DLV-012 | For pickup at a branch, the branch employee shall confirm handover after verifying the customer's identity. | Must |
| DLV-013 | Central Administration shall define the permitted number of delivery attempts. The system shall track unsuccessful attempts against that limit. | Must |
| DLV-014 | When the permitted delivery attempts are exhausted, the order shall be cancelled according to central policy and the products returned to the responsible branch for inspection. | Must |
| DLV-015 | Products returned after failed delivery shall enter pending inspection and shall not become sellable stock automatically. Their disposition shall follow the approved inspection rules. | Must |
| DLV-016 | A customer may dispute a delivery confirmation within a period defined by Central Administration. The order shall be marked disputed while retaining the original delivery confirmation and its history. | Must |
| DLV-017 | The system shall retain available delivery information for dispute review, including employee identity, assignment, timestamps, delivery confirmation, and any evidence available from the workflow. | Must |
| DLV-018 | Central Administration shall review and accept or reject a delivery dispute, recording the decision and reason. A dispute shall not automatically trigger a refund. | Must |
| DLV-019 | A paid pickup order created through a branch shall be treated as a completed sale if the customer does not attend. The centrally defined retention period and subsequent central decision shall be recorded; products shall not be returned to stock automatically and must be inspected before any inventory disposition. | Must |

## 9.3 Main scenarios

### Scenario A — Picking and barcode verification

1. An employee opens a confirmed order assigned to the branch.
2. The employee picks an item and scans its barcode.
3. The system verifies that the barcode belongs to the ordered product.
4. A matching item is recorded as picked.
5. A mismatched barcode is rejected and the discrepancy remains visible for correction/review.

### Scenario B — Assign and complete delivery

1. Central Administration appoints the shift coordinator.
2. The coordinator assigns the delivery task to a delivery employee.
3. The system records the employee, coordinator, assignment time, and status.
4. The delivery employee completes delivery and confirms it manually.
5. The system records the employee identity, confirmation timestamp, and audit history. No OTP or electronic signature is required.

### Scenario C — Failed delivery attempts

1. The delivery employee records an unsuccessful attempt.
2. The system counts it against the centrally configured limit.
3. When the limit is exhausted, the order is cancelled according to policy.
4. The products return to the responsible branch and enter pending inspection.
5. Products become sellable only after the applicable inspection decision.

### Scenario D — Customer disputes delivery

1. The customer submits a dispute within the centrally configured period.
2. The system marks the order disputed without deleting or overwriting the original delivery confirmation.
3. Central Administration reviews the available assignment, employee, timing, status, and evidence.
4. Central Administration accepts or rejects the dispute and records the reason.
5. No refund is issued automatically merely because a dispute was submitted.

## 9.4 Acceptance criteria

- Only confirmed orders assigned to the branch can enter preparation.
- Picked products are verified by barcode; mismatches cannot be accepted as the ordered item.
- Preparation discrepancies remain traceable.
- Central Administration appoints the shift coordinator; the coordinator has no independent administrative or policy-changing authority.
- Delivery assignment and completion record responsible employees and timestamps.
- Manual delivery confirmation does not require OTP or electronic signature.
- Pickup handover requires customer identity verification by the branch employee.
- Failed delivery attempts are counted against the central limit; exhausted attempts cause cancellation and return to branch inspection.
- Returned products are not sellable before inspection.
- A delivery dispute preserves the original confirmation, is decided by Central Administration, and does not automatically trigger a refund.

---

# Unit 10 — Offline Operation and Synchronization

## 10.1 Purpose

Maintain controlled branch operations during connectivity loss, preserve local transactions, synchronize valid operations after connectivity returns, and prevent sales from resuming before reconciliation and Central Administration approval.

## 10.2 Functional requirements

| ID | Functional requirement | Priority |
|---|---|---|
| OFF-001 | Each branch shall use its latest locally available centrally approved data while operating within the centrally configured Offline time limit. | Must |
| OFF-002 | The Offline time limit shall be centrally configurable and apply uniformly to all branches. | Must |
| OFF-003 | Offline login shall remain valid for no more than 24 hours after the last successful central verification. After that period, local login shall not authorize further Offline operation until central verification succeeds. | Must |
| OFF-004 | During an allowed Offline period, the branch shall use the last locally approved base prices and promotions. Expired promotions shall stop applying and the last locally approved base price shall apply. | Must |
| OFF-005 | Branch users shall not create or modify prices, promotions, or central policies while Offline. | Must |
| OFF-006 | When synchronization detects a conflict, the conflicting data/operation shall be isolated and referred to Central Administration. The system shall not resolve business conflicts automatically. | Must |
| OFF-007 | Valid operations that are not affected by a conflict shall continue to synchronize; one conflict shall not block unrelated valid operations. | Must |
| OFF-008 | The system shall prevent duplicate processing of an operation during synchronization or retry and shall not lose an operation when synchronization is interrupted. | Must |
| OFF-009 | If synchronization is interrupted, it shall resume from the last successful point after connectivity returns, rather than restarting all completed work. | Must |
| OFF-010 | Central Administration shall have visibility into branch connectivity, last successful synchronization, pending operations, failed operations, and unresolved conflicts. | Must |
| OFF-011 | Conflicts affecting sales shall trigger an immediate Central Administration alert identifying the branch and affected product/operation. Other conflicts shall appear in the conflict list without an urgent alert. | Must |
| OFF-012 | The system shall record conflict discovery time, resolution time, approving employee, decision, and relevant operation history. | Must |
| OFF-013 | When the approved Offline time limit expires, the branch shall stop creating new sales and issuing invoices. Restoration of internet connectivity alone shall not authorize sales to resume. | Must |
| OFF-014 | During sales-stop mode, the branch may view local operational data and reports in read-only mode, review pending operations, synchronization errors and audit history, and save permitted non-posting drafts or notes. | Must |
| OFF-015 | During sales-stop mode, the branch shall not create sales/invoices, change prices or promotions, approve or post inventory adjustments, approve disposal, complete returns or refunds requiring central approval, or change users, permissions, or policies. | Must |
| OFF-016 | Before sales resume, all pending local operations shall be submitted and acknowledged; failed, pending, or conflicting operations shall be resolved; duplicate processing shall be ruled out; inventory and financial reconciliation shall be completed; and the branch shall receive updated approved central data. | Must |
| OFF-017 | Central Administration shall explicitly approve synchronization completion and authorize the branch to resume sales. | Must |
| OFF-018 | If any required synchronization, conflict resolution, reconciliation, data refresh, or central approval condition is incomplete, the branch shall remain in sales-stop mode and the system shall display the reason. | Must |
| OFF-019 | Synchronization shall preserve the historical values of completed transactions. Later price, promotion, or master-data changes shall not rewrite completed invoices or financial records. | Must |
| OFF-020 | The system shall retain a synchronization history sufficient to trace submitted operations, acknowledgements, failures, retries, conflicts, resolutions, and the actor responsible for each resolution. | Must |

## 10.3 Main scenarios

### Scenario A — Branch operates within the allowed Offline period

1. Connectivity is lost.
2. The branch continues only operations permitted by the approved Offline policy and within the configured time limit.
3. It uses the latest locally approved prices and promotions.
4. An expired promotion is no longer applied; the last approved base price is used.
5. Branch users cannot change centrally managed prices, promotions, or policies.

### Scenario B — Offline time limit expires

1. The centrally configured Offline time limit is reached.
2. The system stops new sales and invoice issuance.
3. Authorized users may view local information and reports in read-only mode, review pending operations, synchronization errors, and audit history, and record permitted non-posting drafts or notes.
4. The branch cannot resume sales merely because connectivity returns.

### Scenario C — Synchronization with a conflict

1. Connectivity is restored and synchronization begins automatically.
2. The branch submits pending operations.
3. The system processes valid operations and isolates only operations that have conflicts.
4. Conflicts are referred to Central Administration; no automatic business conflict resolution is applied.
5. Valid operations continue to synchronize without being blocked by unrelated conflicts.
6. If synchronization is interrupted, it resumes from the last successful point without duplicate processing or loss.

### Scenario D — Resume sales after synchronization

1. All pending operations are submitted and acknowledged.
2. Failed and conflicting operations are resolved.
3. Inventory and financial reconciliation is completed.
4. The branch receives updated approved central data.
5. Central Administration reviews and explicitly approves synchronization completion.
6. Sales resume only after that authorization.

## 10.4 Acceptance criteria

- The Offline time limit is centrally configurable and uniform across branches.
- Branches use only the latest locally approved prices/promotions within the permitted Offline period.
- Expired promotions are not applied Offline; the last approved base price is used.
- Branch users cannot change prices, promotions, or policies Offline.
- Synchronization isolates conflicts without blocking unrelated valid operations.
- Retrying or resuming synchronization does not duplicate or lose operations.
- Central Administration can see branch connectivity, last synchronization, pending/failed operations, and conflicts.
- Sales stop when the Offline time limit expires.
- Connectivity restoration alone does not resume sales.
- Sales remain stopped until pending operations are acknowledged, failures and conflicts are resolved, reconciliation is complete, central data is refreshed, and Central Administration explicitly authorizes resumption.
- If any resumption condition is incomplete, the system keeps sales stopped and explains why.
- Completed historical transactions are not rewritten by later synchronization updates.

---

## 10.5 Traceability and boundaries

- This part records the approved online-store, delivery, customer, and Offline decisions, including WEB-01–WEB-19, DLV-01–DLV-04, ADR-01–ADR-04, ORD-01–ORD-02, OFF-01–OFF-11, and closed GAP-05 through GAP-14 where applicable.
- The 15-minute payment reservation applies to online orders for delivery and branch pickup. The separately approved five-hour rule applies only to unpaid pickup orders created through a branch; paid branch-created pickup orders follow their separately approved policy.
- Customer order notifications (website/email only) are distinct from internal operational alerts, whose approved channels are in Unit 12.
- Customer delivery disputes preserve the original delivery confirmation and require Central Administration review; a dispute alone does not trigger a refund.
- Detailed APIs, database schema, local synchronization protocol, conflict-resolution tooling, and user-interface design belong to technical design.
