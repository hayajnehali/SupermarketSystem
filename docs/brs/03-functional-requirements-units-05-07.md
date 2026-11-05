# BRS — Functional Requirements: Units 05–07

**Status:** Approved by the project owner  
**Scope:** Unit 05 — Pricing, Promotions and Discounts; Unit 06 — POS, Sales and Invoices; Unit 07 — Payments, Refunds and Returns  
**Basis:** Approved business decisions, decision log, operational GAPs, and recommendations P11–P20. This document defines business behavior, not technical implementation.

---

## 1. Unit 05 — Pricing, Promotions and Discounts

### 1.1 Purpose
Ensure that all branches apply centrally approved prices, promotions and coupons consistently, with a traceable history and predictable behavior during connectivity loss.

### 1.2 Functional requirements

- **PRC-01:** Only Central Administration may create, approve, activate, schedule, modify or deactivate base prices and promotions.
- **PRC-02:** The approved base price is uniform across branches unless Central Administration explicitly approves a defined exception.
- **PRC-03:** A price change may have an effective start and end date/time. The system applies it only within that validity period.
- **PRC-04:** The system retains the history of every price and promotion change, including previous and new values, effective period, actor, approval and timestamp.
- **PRC-05:** The system supports product-level promotions, basket-level promotions and coupons, subject to centrally configured eligibility, validity and usage limits.
- **PRC-06:** Promotion stacking and conflict resolution follow the already approved business rules; a cashier cannot override them.
- **PRC-07:** Coupons must be validated against their validity period, eligibility conditions and usage limits before being applied.
- **PRC-08:** During connectivity loss, a branch uses only its latest locally available centrally approved pricing and promotion data. Branch users cannot create or change prices or promotions.
- **PRC-09:** An expired promotion is no longer applied. The applicable base price is used when no other valid promotion applies.
- **PRC-10:** A later price change must not alter the prices, discounts or totals stored on a completed historical invoice.
- **PRC-11:** Monetary calculations use the configured currency precision. For JOD, amounts are represented to three decimal places. Weight quantities may be represented to three decimal places, according to the product's configured unit.
- **PRC-12:** If a price or promotion becomes invalid while a sale is in progress, the system must refresh and recalculate the affected cart before completion, show the changed price/discount/total to the cashier, and require confirmation. The cashier cannot complete the sale using an expired price or promotion.

### 1.3 Main scenarios

1. Central Administration schedules a price change. Branches apply the new price from its approved effective time.
2. A cashier scans a product while a promotion is valid. The POS applies the eligible promotion automatically.
3. A promotion expires while the product remains in an open cart. Before payment, the POS recalculates the cart and displays the changed total for confirmation.
4. A branch loses connectivity. It continues using its latest approved local pricing data within the permitted Offline operating period.
5. Central Administration changes a product price after an invoice is completed. The historical invoice remains unchanged.

### 1.4 Acceptance criteria

- Unauthorized branch users cannot change centrally managed prices or promotions.
- A scheduled price or promotion is not applied before its effective start or after its effective end.
- Every price/promotion change can be traced to its actor, approval and effective period.
- Expired promotions are not applied to a completed sale.
- A cart affected by a price/promotion expiry is recalculated and presented for cashier confirmation before payment.
- Historical invoices retain their original monetary values and discounts.
- JOD calculations preserve three decimal places; other currencies follow their configured currency precision.

---

## 2. Unit 06 — POS, Sales and Invoices

### 2.1 Purpose
Provide controlled point-of-sale operations with accurate stock, pricing, payment, invoice numbering and cashier-shift accountability.

### 2.2 Functional requirements

- **POS-01:** A cashier must open a shift before starting sales and record the opening cash amount.
- **POS-02:** The cashier can find products by barcode, name or SKU.
- **POS-03:** The POS supports sale by piece and by configured weight unit, including up to three decimal places where permitted.
- **POS-04:** Composite products are sold according to their approved component definitions, and their component stock is deducted according to the approved inventory rules.
- **POS-05:** The POS obtains applicable prices, promotions and discounts from centrally approved data and applies them automatically.
- **POS-06:** A cashier may suspend a sale and resume it later.
- **POS-07:** When a suspended sale is resumed, the POS recalculates prices and promotions using the rules and approved data applicable at completion time. It displays any changes and requires cashier confirmation before completion.
- **POS-08:** A cashier cannot manually change a product's approved price or grant an unauthorized manual discount.
- **POS-09:** One invoice may be paid using multiple payment methods, provided the full invoice total is paid. This must not create a credit balance or deferred amount.
- **POS-10:** Each invoice has a branch/year-based sequential number, for example `BR01-YYYY-000001`, and a separate internal GUID. The sequence year is stored as `SequenceYear`.
- **POS-11:** The system prevents duplicate invoice numbers within the applicable branch and sequence year.
- **POS-12:** A sale may be cancelled before payment according to the user's permissions. Cancelling a paid invoice requires Central Administration approval and an auditable reason.
- **POS-13:** Reprinting an invoice produces an additional copy of the same invoice. It does not create a new sale, invoice number or stock movement.
- **POS-14:** The system records the expected and counted cash drawer amounts at shift closing, calculates any difference and records the reason when provided.
- **POS-15:** Cashiers cannot edit the expected drawer balance or delete a recorded cash difference.
- **POS-16:** Offline sales follow the approved Offline time limit and operating rules. Sales stop when that limit expires; connectivity restoration alone does not authorize sales to resume.
- **POS-17:** A sale cannot exceed available sellable stock, including stock reserved for website orders or otherwise unavailable under approved inventory rules.
- **POS-18:** A completed sale records the applicable product price, discount, tax placeholder where applicable, payment allocation and other invoice values as a historical snapshot.

### 2.3 Main scenarios

1. The cashier opens a shift, records the opening cash amount and begins scanning products.
2. The cashier suspends a cart. When it is resumed, the POS recalculates current applicable prices and shows any changes before confirmation.
3. The customer pays part in cash and the remainder electronically. The POS completes the sale only after the full total is confirmed as paid.
4. The cashier requests an invoice reprint. The same invoice is printed with a copy indication and no new sale is created.
5. The cashier closes the shift. The POS calculates the cash difference and preserves it for review.
6. A branch reaches the approved Offline time limit. The POS blocks new sales until synchronization, reconciliation and Central Administration approval are completed.

### 2.4 Acceptance criteria

- A sale cannot start without an open cashier shift.
- Product lookup supports barcode, name and SKU.
- The POS applies centrally approved prices and promotions and does not allow unauthorized price overrides.
- A resumed or price-expired cart shows recalculated values and requires confirmation before payment.
- Split payments are accepted only when their confirmed total equals the invoice total; no unpaid balance is carried forward.
- Invoice numbering follows the branch/year sequence and is unique.
- Reprinting does not create a sale, invoice number or stock movement.
- Cash differences are recorded and cannot be deleted or altered by the cashier.
- Paid invoice cancellation requires Central Administration approval.
- Sales stop when the approved Offline limit expires.

---

## 3. Unit 07 — Payments, Refunds and Returns

### 3.1 Purpose
Ensure payment completion is reliable and that refunds and returned goods are processed against traceable original transactions and approved inspection rules.

### 3.2 Functional requirements

- **PAY-01:** The POS supports approved cash and electronic payment methods.
- **PAY-02:** An electronic payment is considered successful only after reliable confirmation from the payment provider. A client-side response or an unverified pending state is not sufficient.
- **PAY-03:** A sale cannot be completed while any required payment remains pending, failed or unconfirmed.
- **PAY-04:** The system supports full and partial refunds. The cumulative refunded amount must not exceed the amount successfully paid.
- **PAY-05:** Refunds are returned to the original payment method whenever possible. An exception requires Central Administration approval and a recorded reason.
- **PAY-06:** A normal return must reference the original invoice. A return without an original invoice is an exception and must be referred to Central Administration.
- **PAY-07:** A return is recorded by an authorized employee and linked to the original sale and returned item/quantity.
- **PAY-08:** Returned goods enter a pending-inspection state. They must not become sellable stock until inspection confirms they are eligible for return to stock.
- **PAY-09:** Goods confirmed as sellable may return to stock under the approved inventory and batch-cost rules. Goods with an unresolved condition remain isolated. Damaged or expired goods are isolated and submitted for Central Administration approval for disposal.
- **PAY-10:** Partial returns are allowed for products sold by weight when the product's configured unit and rules permit it. The returned weight and remaining quantity must be validated and recorded. Piece-based products are returned in whole sellable units; fractional piece returns are not allowed.
- **PAY-11:** An exchange is recorded as a linked return and replacement sale, preserving the financial and stock records of both operations.
- **PAY-12:** Payment, refund and return statuses are tracked independently where required. A pending, failed or partial refund must not be misrepresented as a completed refund.
- **PAY-13:** Refund processing must be idempotent and traceable so that repeated provider notifications or user retries do not cause duplicate refunds.
- **PAY-14:** The system preserves the original sale price, discount, payment allocation and applicable batch-cost reference when processing a return.

### 3.3 Main scenarios

1. The customer pays electronically. The POS completes the sale only after trusted provider confirmation.
2. A payment remains pending or fails. The POS does not mark the sale as paid or complete.
3. The customer returns an item with its original invoice. The authorized employee records the return and the item enters pending inspection.
4. Inspection confirms the item is sellable. It is returned to stock according to the applicable inventory rules.
5. Inspection finds the item damaged or expired. It remains isolated until Central Administration approves disposal.
6. A customer returns part of a product sold by weight. The employee records the returned weight and the system validates it against the original sold quantity and previous returns.
7. A refund to the original payment method is unavailable. The employee requests an exception; the refund method is changed only after Central Administration approval.
8. A provider sends the same payment/refund notification more than once. The system processes the underlying financial operation only once.

### 3.4 Acceptance criteria

- An electronic payment is not successful without trusted provider confirmation.
- No sale is completed with a pending, failed or unconfirmed required payment.
- Full and partial refunds cannot exceed the successfully paid amount.
- Refunds use the original payment method unless an approved exception is recorded.
- Normal returns require the original invoice; exceptions are referred to Central Administration.
- Returned goods cannot become sellable stock before inspection.
- Damaged/expired goods remain isolated until disposal approval.
- Weight-based partial returns validate the returned and remaining quantities.
- Piece-based products cannot be returned in fractional units.
- Exchanges create linked return and replacement-sale records.
- Repeated payment-provider notifications cannot create duplicate financial operations.

---

## 4. Traceability and boundaries

- This document carries forward the approved decisions P11–P20 and the related operational GAP decisions. It does not replace or reopen them.
- Website order payment and refund workflows remain governed by the approved website/order requirements; this unit defines the shared payment and return controls where applicable.
- Tax and e-invoicing remain placeholders only, as previously approved. This document does not introduce tax calculations or legal e-invoice integration.
- Offline limits, synchronization, conflict resolution and resumption approval remain governed by Unit 10 and the approved Offline GAP decisions.
- Currency precision is configured by currency; JOD uses three decimal places.
- This document defines business requirements and acceptance behavior. Detailed API contracts, database schema, UI design and implementation choices belong to the technical design phase.
