# TD-32H — Central Payments, POS Tender Evidence and Cash Refund Physical Schema Decisions

**Version:** 0.1  
**Status:** Delegated design approval — approved for physical mapping.  
**Date:** 2026-11-28  
**Depends on:** TD-21, TD-26, TD-30, TD-31 and TD-32.

## 1. Payment authority by channel

- Central owns provider configuration, online payment initiation, provider callbacks/events, reconciliation and online payment status.
- Branch owns POS payment transaction records and cashier-entered independent terminal outcomes.
- The POS bank terminal is independent: no app SDK/API, webhook or automatic verification. The cashier records the result and optional bank receipt/reference.
- Branch does not store provider secrets, receive provider callbacks or claim an independent terminal result was provider-verified.
- Supplier payments are not part of this module; they remain Central-only under Procurement with SupplierPaymentAllocations.

## 2. Payment data model

### 2.1 Central-owned configuration and provider events
- `payments.PaymentProviders`: centrally approved provider identity/status and supported capabilities.
- `payments.PaymentProviderConfigurations`: non-secret configuration plus protected secret references/key-vault identifiers. Never store plaintext credentials.
- `payments.PaymentMethods`: centrally enabled cash/electronic methods and approved configuration; Branch receives a read-only approved projection.
- `payments.PaymentProviderEvents`: durable callback receipt, provider/account-scoped unique event ID, verified signature result, sanitized payload/hash, received/processed times and processing outcome. Never store PAN, CVV, track data or sensitive authentication data.

### 2.2 PaymentTransactions and PaymentAttempts
- `PaymentTransactions` authority follows the channel: Branch for POS, Central for online.
- A transaction records stable OperationId, channel, method, amount, currency, state, source invoice/order GUID, timestamps and final result.
- `PaymentAttempts` records each attempt and its status/reference. For POS electronic terminal attempts, it stores only the cashier-entered outcome and optional bank reference; it is not a provider attempt and does not invoke provider APIs.
- One sale may have multiple successful tender allocations (split tender). The sale completes only when confirmed successful allocations cover the final invoice amount.
- Amounts use decimal(18,3); JOD is the enabled first-release currency; final invoice payable uses TD-31 rounding.

## 3. Online payment workflow

1. Central creates the payment transaction/attempt and reserves the order/payment window for 15 minutes.
2. The provider initiation uses a stable application OperationId and provider/account-scoped idempotency key.
3. Provider callback signature/authenticity is verified before processing.
4. Provider event IDs are unique within provider/account scope. Duplicate callbacks return the prior outcome.
5. Callback arrival order is not trusted. Only valid forward state transitions are accepted; stale/conflicting events are retained for audit/reconciliation and cannot regress a terminal state.
6. An ambiguous timeout is resolved using the original provider reference/idempotency key. Do not create a new attempt until the original outcome is resolved or explicitly classified as a separate attempt.
7. A payment confirmed after the 15-minute reservation has expired remains linked to the original cancelled order; it does not reserve stock or start fulfillment and follows the approved late-payment refund/notification handling.
8. Provider event receipt, processing result and business payment state are independently traceable.

## 4. POS tender workflow

- Cash and independent electronic terminal tender are recorded at the Branch.
- Cash tender and the related CashDrawerTransaction commit in the same transaction as invoice completion.
- Electronic terminal tender requires an authorized cashier confirmation, amount/method, attempt time and optional bank receipt/reference. It does not call the Central provider adapter.
- Payment transaction, attempt, allocation and invoice state are committed atomically. A failed/declined attempt leaves the sale incomplete and issues no stock.
- OperationId and source-purpose uniqueness prevent duplicate tender posting.

## 5. Customer refunds — cash payout only

1. Every refund references the original invoice/order and original successful PaymentTransaction(s).
2. All customer refunds are physically paid in cash from the responsible Branch cash drawer, regardless of original tender.
3. If any original successful tender was electronic, Central approval and a recorded reason are mandatory before Branch cash payout.
4. Split-tender refund allocation is proportional to the original successful tender amounts. Currency precision is preserved; any final rounding remainder is assigned deterministically to the last eligible allocation in stable order without exceeding its tender cap.
5. Refund amount cannot exceed the original invoice paid amount or the remaining refundable amount of each original tender.
6. Refund creation, refund-line/allocation rows, cash drawer posting, stock disposition (where applicable), ProcessedOperations and Outbox are committed in the appropriate local transaction. For online refunds, Central decision/command and Branch payout are separate local transactions with durable acknowledgement.
7. No provider refund API/SDK is invoked. Electronic-source refund is still paid in cash after Central approval.
8. `RefundAttempts` is not used for provider refund execution in the first release. Keep the table outside the initial physical schema unless a later approved workflow requires a real external refund attempt.

## 6. Refund concurrency

- Each original invoice/payment has a concurrency-controlled refund-control state.
- Within one transaction, re-read/conditionally update the refund-control row, verify remaining invoice and per-tender refundable caps, insert refund and allocations, and record the CashDrawerTransaction.
- A stale rowversion or failed cap predicate rejects/reloads/re-evaluates the request. Do not retry blindly.
- Concurrent successful refunds cannot exceed the original paid amount or any original tender's remaining cap.
- Duplicate OperationId returns the original result; a different payload under the same key is an integrity conflict.
- Reversal/correction is a compensating record; do not delete a successful refund or drawer event.

## 7. Provider reconciliation

- Central owns reconciliation headers/lines and provider settlement evidence.
- Provider settlement imports are idempotent and retain source file/API reference, hash, time zone and sanitized processing metadata.
- A reconciliation mismatch is reviewed and resolved by an authorized Central user; it does not silently rewrite payment transactions.
- Provider callback signatures, event mapping and retry policy are provider-specific adapter contracts and must be validated against the selected provider's official documentation before production.

## 8. Required constraints and indexes

- Unique provider/account/external event ID for PaymentProviderEvents.
- Unique provider-scoped idempotency key and external payment reference where guaranteed by the provider.
- Unique OperationId at the correct channel/business-effect scope.
- Valid status/type CHECK constraints and non-negative/positive amount constraints as appropriate.
- Refund cap control row uses rowversion and atomic conditional update.
- CashDrawerTransaction references are unique by source operation/purpose to prevent duplicate drawer postings.
- All FKs are local. Branch/online cross-database IDs are correlation GUIDs only.

## 9. Required tests

- Duplicate and out-of-order provider callbacks; invalid signature; same event ID with different payload hash.
- Ambiguous provider timeout and reconciliation before retry.
- Late successful callback after the 15-minute order/payment reservation expires.
- POS independent terminal success/failure and duplicate cashier confirmation.
- Split tender sale completion and proportional refund allocation with deterministic rounding remainder.
- Concurrent refund requests against invoice and tender caps.
- Cash drawer and refund atomicity; duplicate refund/command replay.
- Central-approved online refund command and Branch cash payout acknowledgement.
- No provider refund API call in any customer refund path.

## 10. Approval record

The owner delegated detailed design decisions on 2026-11-28. This Central/Branch payment and cash-refund physical design is approved for mapping. Provider-specific cryptographic and API details remain an adapter implementation gate.
