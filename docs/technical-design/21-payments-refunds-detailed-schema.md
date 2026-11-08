# TD-21 — Detailed Logical Schema: Payments, Refunds and Reconciliation

**Version:** 0.1  
**Status:** Proposed for owner review  
**Date:** 2026-11-07  
**Scope:** Cash/electronic payment methods, payment provider configuration, POS and online payment transactions, attempts, trusted provider events, partial/full refunds, refund-method exceptions and provider reconciliation.  
**Depends on:** Approved BRS Unit 07, PMT-01–PMT-16, P11–P20, approved online checkout/payment rules, TD-03, TD-04, TD-05, TD-09, TD-12, TD-13 and TD-14.

> This is a logical/physical schema proposal, not executable DDL, EF Core code or a migration. Provider selection and exact provider API are not invented. Tax/e-invoicing remain placeholders only.

## 1. Payment invariants

1. Every PK is sequential application-generated `Guid` / SQL Server `uniqueidentifier`; no integer identity PK.
2. Monetary values use `decimal(18,3)` for JOD storage and internal calculations. Final JOD invoice amounts use the approved two-decimal rounding; the technical half-rounding tie-breaker remains open until implementation.
3. An electronic payment is successful only after trusted server-side provider verification/callback. Client-side UI response is never sufficient.
4. An invoice/order cannot be marked paid or completed while any required payment remains pending, failed or unconfirmed.
5. One invoice may have multiple payment allocations. Confirmed allocations must cover the entire invoice amount; no credit balance or unpaid remainder is created.
6. Full and partial refunds are allowed. Cumulative successful refunds cannot exceed the amount successfully paid on the original payment transaction.
7. All customer refunds are paid in cash from the Branch cash drawer, regardless of the original tender method. If the original payment included an electronic tender, the cash refund requires prior Central approval and a recorded reason. The original payment transactions remain linked for refund-limit enforcement and audit.
8. Payment, invoice/order, return and refund statuses are independent. A partial/pending/failed refund is not reported as completed.
9. Payment and refund operations are idempotent. Duplicate callbacks, retries or repeated user requests cannot create duplicate financial effects.
10. No PAN, CVV, full card number, provider secret or reusable credential is stored in application database rows. Use provider tokens and protected secret references.

## 2. Ownership and placement

| Table | Central DB | Each Branch DB |
|---|---|---|
| PaymentProviders, PaymentProviderConfigurations | Authoritative | No provider secrets/configuration authority |
| PaymentMethods | Authoritative | Approved read-only method projection |
| PaymentTransactions | Authoritative for online orders; consolidated history for POS | Authoritative POS transaction record |
| PaymentAttempts | Authoritative provider attempt history | Local attempt reference where POS terminal integration requires it |
| PaymentProviderEvents | Authoritative webhook/callback inbox | No |
| Refunds | Authoritative online/electronic refund workflow; synchronized history | Authoritative local cash refund record |
| RefundLines | Same authority as Refunds | Same |
| RefundAttempts | Central provider refund attempts | No provider refund execution authority |
| RefundMethodExceptionRequests | Central review copy | Branch request origin where initiated at POS |
| RefundMethodExceptionDecisions | Authoritative | No |
| PaymentReconciliations, PaymentReconciliationLines | Authoritative | No |

POS and online transaction records preserve their source GUIDs when synchronized. Provider events and external references are correlated by GUID/string identifiers; no cross-database FK is created.

## 3. Providers and payment methods

### 3.1 `payments.PaymentProviders` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PaymentProviderId | uniqueidentifier | No | PK; sequential Guid |
| ProviderCode | nvarchar(50) | No | Unique stable code |
| Name | nvarchar(150) | No | Provider display name |
| ProviderStatus | tinyint | No | Pending / Active / Suspended |
| SupportedCurrencies | nvarchar(max) | Yes | Validated structured list |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor |
| UpdatedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- Unique ProviderCode.
- Validate SupportedCurrencies as structured data; only configured currencies can be used.
- Provider status controls new payment initiation; historical transactions remain available.

### 3.2 `payments.PaymentProviderConfigurations` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PaymentProviderConfigurationId | uniqueidentifier | No | PK; sequential Guid |
| PaymentProviderId | uniqueidentifier | No | FK → PaymentProviders |
| Environment | tinyint | No | Sandbox / Production |
| MerchantAccountReference | nvarchar(150) | Yes | Non-secret merchant account reference |
| PublicConfiguration | nvarchar(max) | Yes | Validated non-secret settings |
| CredentialSecretReference | nvarchar(300) | No | Reference to protected secret store |
| WebhookSecretReference | nvarchar(300) | Yes | Reference to protected webhook secret |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| IsActive | bit | No | Current config |
| UpdatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- FK Provider; restrict delete.
- Unique provider + environment + effective start.
- Never store actual secret values in SQL. Rotate by creating a new configuration version and protected secret reference.

### 3.3 `payments.PaymentMethods` — Central; Branch projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PaymentMethodId | uniqueidentifier | No | PK; sequential Guid |
| PaymentMethodCode | nvarchar(40) | No | Unique stable code |
| Name | nvarchar(120) | No | Display name |
| MethodType | tinyint | No | Cash / Electronic |
| PaymentProviderId | uniqueidentifier | Yes | FK → PaymentProviders for electronic methods |
| IsAllowedOffline | bit | No | Centrally approved Offline capability |
| IsActive | bit | No | Deactivation only |
| CreatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- Unique PaymentMethodCode.
- Cash does not require a provider; electronic method requires an active provider.
- Offline use is controlled centrally; no Branch can enable an otherwise disallowed method.

## 4. Payment transactions and attempts

### 4.1 `payments.PaymentTransactions` — Branch for POS; Central for online orders

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PaymentTransactionId | uniqueidentifier | No | PK; sequential Guid |
| PaymentMethodId | uniqueidentifier | No | Approved method GUID |
| PaymentProviderId | uniqueidentifier | Yes | Provider GUID for electronic method |
| SourceType | tinyint | No | POSInvoice / OnlineOrder |
| SourceTransactionId | uniqueidentifier | No | Invoice or Order GUID |
| BranchId | uniqueidentifier | Yes | Branch origin for POS; central reference for online fulfillment branch |
| Amount | decimal(18,3) | No | Positive requested amount |
| CurrencyCode | char(3) | No | JOD or configured currency |
| TransactionStatus | tinyint | No | Initiated / Pending / Confirmed / Failed / Cancelled |
| ExternalReference | nvarchar(150) | Yes | Provider transaction reference |
| IdempotencyKey | uniqueidentifier | No | Stable operation identity |
| InitiatedAtUtc | datetime2(3) | No | UTC |
| ConfirmedAtUtc | datetime2(3) | Yes | UTC trusted confirmation |
| FailureCode | nvarchar(80) | Yes | Provider/internal failure code |
| FailureDescription | nvarchar(500) | Yes | Sanitized failure description |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique IdempotencyKey within its transaction authority.
- Unique filtered ProviderId + ExternalReference when ExternalReference is non-null.
- Amount > 0; currency must be enabled.
- POS source links to Branch invoice; online source links to Central order. Cross-database copies use GUID correlation only.
- A transaction reaches Confirmed only through trusted backend provider verification, verified callback or an approved cash confirmation.
- Refund status is not stored as a terminal PaymentTransaction status; refunds are separate records.

### 4.2 `payments.PaymentAttempts` — Transaction authority

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PaymentAttemptId | uniqueidentifier | No | PK; sequential Guid |
| PaymentTransactionId | uniqueidentifier | No | FK → local PaymentTransactions |
| AttemptNumber | int | No | Positive sequence per transaction |
| PaymentProviderId | uniqueidentifier | Yes | Provider GUID |
| ProviderRequestReference | nvarchar(150) | Yes | Provider request reference |
| ProviderTransactionReference | nvarchar(150) | Yes | Provider transaction reference |
| AttemptStatus | tinyint | No | Created / Pending / Confirmed / Failed / TimedOut |
| StartedAtUtc | datetime2(3) | No | UTC |
| CompletedAtUtc | datetime2(3) | Yes | UTC |
| ResponseCode | nvarchar(80) | Yes | Sanitized response code |
| ResponseSummary | nvarchar(500) | Yes | Sanitized response text |
| IdempotencyKey | uniqueidentifier | No | Stable attempt key |

**Constraints**
- Unique PaymentTransactionId + AttemptNumber.
- Unique IdempotencyKey.
- Do not store sensitive cardholder data or raw authentication secrets.
- Retries create new attempt rows under the same PaymentTransactionId; they do not create a second financial transaction.

### 4.3 `payments.PaymentProviderEvents` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PaymentProviderEventId | uniqueidentifier | No | PK; sequential Guid |
| PaymentProviderId | uniqueidentifier | No | FK → PaymentProviders |
| ProviderEventId | nvarchar(180) | No | Provider's unique event ID |
| ProviderTransactionReference | nvarchar(150) | Yes | Provider transaction reference |
| EventType | nvarchar(80) | No | Provider event type |
| PayloadHash | binary(32) | No | SHA-256 of exact received payload |
| SanitizedPayload | nvarchar(max) | Yes | Sanitized payload only; no card secrets |
| SignatureVerified | bit | No | Server-side signature/authenticity result |
| ReceivedAtUtc | datetime2(3) | No | UTC |
| ProcessedAtUtc | datetime2(3) | Yes | UTC |
| ProcessingStatus | tinyint | No | Received / Verified / Processed / Rejected / Failed |
| ProcessingError | nvarchar(1000) | Yes | Sanitized error |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique PaymentProviderId + ProviderEventId; duplicate webhook events resolve to the original event result.
- Provider signature/authenticity verification occurs before the event can change a transaction.
- Event processing is idempotent and correlated to PaymentTransactionId through provider references; no client callback is trusted by itself.

## 5. Refunds and refund-method exceptions

### 5.1 `payments.Refunds` — Central for electronic/online; Branch for permitted cash refund

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| RefundId | uniqueidentifier | No | PK; sequential Guid |
| OriginalPaymentTransactionId | uniqueidentifier | No | Original successful payment GUID |
| SourceType | tinyint | No | POSReturn / OnlineOrderReturn / PaidInvoiceCancellation |
| SourceTransactionId | uniqueidentifier | No | Return or order/invoice GUID |
| RefundAmount | decimal(18,3) | No | Positive amount |
| CurrencyCode | char(3) | No | Original payment currency |
| OriginalPaymentMethodId | uniqueidentifier | No | Original tender method |
| RefundPaymentMethodId | uniqueidentifier | No | Cash only; Branch cash drawer |
| RefundStatus | tinyint | No | Requested / PendingApproval / Processing / PartiallyRefunded / Completed / Failed / Cancelled |
| ExceptionDecisionId | uniqueidentifier | Yes | Central exception decision GUID when method differs |
| RequestedByUserId | uniqueidentifier | No | Actor |
| RequestedAtUtc | datetime2(3) | No | UTC |
| CompletedAtUtc | datetime2(3) | Yes | UTC |
| Reason | nvarchar(1000) | No | Refund reason |
| IdempotencyKey | uniqueidentifier | No | Stable refund operation identity |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique IdempotencyKey per refund authority.
- FK to local PaymentTransaction where the original payment is in the same database; cross-database original payment is a GUID correlation only.
- RefundPaymentMethodId must identify the approved Cash method. A refund against an invoice originally paid wholly or partly by an electronic method requires a valid Central approval decision and recorded reason before cash is paid.
- Refunds remain linked to their original successful payment transaction(s) for cumulative refund-limit enforcement. Cash is the payout method; it does not change the original tender history.
- A refund is not paid until the required Central approval has been received and validated by the Branch.
- Cumulative successful refunds per original payment transaction cannot exceed the successfully paid amount; enforce with transaction-safe locking/conditional update.
- Partial refunds are represented as separate refund records and/or partial status transitions; never increase successful refunded total beyond the original paid amount.
- Every POS/customer refund is paid in cash and recorded in Branch CashDrawerTransactions. No POS refund is paid back to a bank card or another electronic instrument.
- Online order refunds are also fulfilled as cash at the responsible Branch only after the applicable Central approval and branch authorization workflow; the exact customer handover workflow must be defined in the fulfillment procedure.

### 5.2 `payments.RefundLines` — Same authority as Refunds

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| RefundLineId | uniqueidentifier | No | PK; sequential Guid |
| RefundId | uniqueidentifier | No | FK → Refunds |
| SourceLineId | uniqueidentifier | Yes | Original invoice/order/return line GUID |
| RefundedQuantity | decimal(18,3) | Yes | Optional quantity where line-based |
| LineRefundAmount | decimal(18,3) | No | Positive allocated amount |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- Refund → RefundLines is 1:N when line allocation applies.
- Sum of line refund amounts must equal RefundAmount; if a refund is not line-based, use a single explicit adjustment line with reason.
- Returned product quantities and eligible refund amounts must be validated against the original invoice/order/return.

### 5.3 `payments.RefundAttempts` — Central provider workflow

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| RefundAttemptId | uniqueidentifier | No | PK; sequential Guid |
| RefundId | uniqueidentifier | No | FK → Central Refunds |
| AttemptNumber | int | No | Positive sequence |
| ProviderRefundReference | nvarchar(150) | Yes | Provider refund reference |
| AttemptStatus | tinyint | No | Created / Pending / Confirmed / Failed / TimedOut |
| IdempotencyKey | uniqueidentifier | No | Stable provider refund attempt key |
| StartedAtUtc | datetime2(3) | No | UTC |
| CompletedAtUtc | datetime2(3) | Yes | UTC |
| ResponseCode | nvarchar(80) | Yes | Sanitized provider response |
| ResponseSummary | nvarchar(500) | Yes | Sanitized provider response |

**Constraints**
- Unique RefundId + AttemptNumber and unique IdempotencyKey.
- Repeated provider notifications/retries cannot create a second successful refund for the same underlying operation.

### 5.4 `payments.RefundMethodExceptionRequests` and `RefundMethodExceptionDecisions`

**`RefundMethodExceptionRequests`** — Branch origin or Central online workflow copy

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| RefundMethodExceptionRequestId | uniqueidentifier | No | PK; stable GUID across copies |
| RefundId | uniqueidentifier | No | Refund GUID |
| OriginalPaymentMethodId | uniqueidentifier | No | Original method |
| RequestedRefundMethodId | uniqueidentifier | No | Proposed alternate method |
| Reason | nvarchar(1000) | No | Required reason |
| RequestedByUserId | uniqueidentifier | No | Actor |
| RequestedAtUtc | datetime2(3) | No | UTC |
| RequestStatus | tinyint | No | Submitted / Approved / Rejected |
| RowVersion | rowversion | No | Concurrency token |

**`RefundMethodExceptionDecisions`** — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| RefundMethodExceptionDecisionId | uniqueidentifier | No | PK; sequential Guid |
| RefundMethodExceptionRequestId | uniqueidentifier | No | FK → Central request copy |
| Decision | tinyint | No | Approve / Reject |
| ApprovedRefundMethodId | uniqueidentifier | Yes | Required when approved |
| Reason | nvarchar(1000) | No | Central decision reason |
| DecidedByUserId | uniqueidentifier | No | Central actor |
| DecidedAtUtc | datetime2(3) | No | UTC |

**Constraints**
- Refund method cannot change before Central approval.
- The approved method must be active and valid for the refund channel.
- A Branch applies the decision only through a valid authenticated command; no local self-approval.

## 6. Provider reconciliation

### 6.1 `payments.PaymentReconciliations` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PaymentReconciliationId | uniqueidentifier | No | PK; sequential Guid |
| PaymentProviderId | uniqueidentifier | No | FK → PaymentProviders |
| SettlementReference | nvarchar(150) | No | Provider settlement batch/reference |
| SettlementDate | date | No | Provider settlement business date |
| CurrencyCode | char(3) | No | Currency |
| ProviderTotalAmount | decimal(18,3) | No | Provider-reported total |
| SystemTotalAmount | decimal(18,3) | No | System-calculated matched total |
| DifferenceAmount | decimal(18,3) | No | Provider minus system |
| ReconciliationStatus | tinyint | No | Imported / Matching / Matched / Difference / Reviewed / Closed |
| ImportedAtUtc | datetime2(3) | No | UTC |
| ReviewedByUserId | uniqueidentifier | Yes | Central reviewer |
| ReviewedAtUtc | datetime2(3) | Yes | UTC |
| ReviewNotes | nvarchar(1000) | Yes | Resolution notes |
| RowVersion | rowversion | No | Concurrency token |

### 6.2 `payments.PaymentReconciliationLines` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PaymentReconciliationLineId | uniqueidentifier | No | PK; sequential Guid |
| PaymentReconciliationId | uniqueidentifier | No | FK → PaymentReconciliations |
| PaymentTransactionId | uniqueidentifier | Yes | Central transaction/projection GUID |
| ProviderTransactionReference | nvarchar(150) | Yes | Provider reference |
| ProviderAmount | decimal(18,3) | No | Provider amount |
| SystemAmount | decimal(18,3) | Yes | Internal amount when matched |
| DifferenceAmount | decimal(18,3) | No | Difference |
| MatchStatus | tinyint | No | Matched / MissingInSystem / MissingInProvider / AmountMismatch / Duplicate |
| ResolutionStatus | tinyint | No | Open / Resolved / AcceptedDifference |
| ResolutionNotes | nvarchar(1000) | Yes | Central resolution |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Reconciliation → lines is 1:N.
- Unique ProviderId + SettlementReference.
- A reconciliation identifies missing/duplicate/mismatched provider transactions and preserves Central review outcome.
- Reconciliation is operational payment control, not a general ledger or accounting module.

## 7. Cross-module transaction boundaries

| Workflow | Owning transaction boundary | Related records |
|---|---|---|
| POS cash payment | Branch DB | PaymentTransaction + PaymentAttempt/confirmation + SalePaymentAllocation + CashDrawerTransaction |
| POS electronic payment | Branch transaction record plus trusted provider confirmation workflow | PaymentTransaction GUID + attempts/provider events + allocation; sale completes only after confirmation |
| Online checkout | Central DB | Order + PaymentTransaction + reservation state; Branch stock reservation is a separate local transaction |
| Provider callback | Central DB | ProviderEvent deduplication + transaction state transition + Outbox/event publication |
| POS/customer cash refund | Branch DB | Refund + RefundLines + original-payment linkage + CashDrawerTransaction + Outbox; Central approval required when original tender includes electronic payment |
| Online order refund | Central approval/workflow plus Branch cash payout | Refund + RefundLines + original-payment linkage + Central decision + Branch CashDrawerTransaction + Outbox; no provider refund execution |
| Refund-method exception | Central approval boundary | Request + Central decision + authorized refund command |
| Provider reconciliation | Central DB | Reconciliation header + lines + reviewed resolution |

No distributed transaction is used. Each local transaction is atomic and cross-site progress is idempotent through the approved Outbox/Inbox/OperationId model.

## 8. Requirement traceability

| Approved requirement/decision | Schema support |
|---|---|
| PMT-01 | PaymentMethods supports cash and electronic methods |
| PMT-02, PMT-03 | PaymentTransactions, Attempts and trusted ProviderEvents |
| PMT-04, PMT-12, PMT-13, PMT-16 | Refunds, RefundLines, RefundAttempts and idempotency constraints |
| PMT-05 | RefundMethodExceptionRequests/Decisions; original method retained by default |
| PMT-06–PMT-11, PMT-14 | Linked return and exchange schema in TD-20; Refunds links to source return/order/invoice |
| PMT-15, P11 | SalePaymentAllocations and PaymentTransactions; full confirmed total required |
| Online 15-minute payment window | Central online PaymentTransactions and order/reservation lifecycle; late callbacks remain traceable |
| FIN/SEC audit requirements | Actor/time/reason plus AuditEntries and SecurityEvents |
| Tax/e-invoicing decision | No tax engine/provider integration is added; placeholders remain outside this schema |

## 9. Technical risks for review

1. Confirm the provider integration topology for POS electronic payments: whether Branch calls provider directly or Central API mediates it. This schema keeps provider secrets and webhook authority in Central and correlates Branch transaction GUIDs.
2. Define the provider-specific idempotency key and external-reference uniqueness rules before integration.
3. Define the accounting allocation of a refund amount across multiple original payment transactions for split-tender invoices. The customer payout remains cash-only; the allocation is needed solely to link the refund to original successful transactions and enforce each transaction's remaining refundable balance.
4. Define the detailed customer cash handover/collection workflow for online-order refunds at the Branch, including identity/receipt evidence and audit requirements.
5. Define the provider event retention and sanitized payload policy; do not retain sensitive cardholder data.
6. Define partial refund retry/reconciliation behavior when provider confirmation is delayed or ambiguous.
7. Define how a Central refund exception decision reaches a disconnected Branch and prevents alternate-method payment before approval.
8. Define provider settlement file/API ingestion format and timezone semantics.

## 10. Approval boundary and next step

TD-21 specifies Payments & Refunds only. It does not create SQL tables, EF Core entities, DDL or migrations. The next detailed group is Online Orders and Customer/Loyalty/Fulfillment, followed by Expenses/Maintenance, Alerts, Audit and Reporting.

**No SQL table, EF Core entity, DDL or migration is created by this document.**
