# TD-22 — Detailed Logical Schema: Online Orders, Customer, Loyalty and Fulfillment

**Version:** 0.1  
**Status:** Proposed for owner review  
**Date:** 2026-11-07  
**Scope:** Customer profiles/verification/duplicate resolution, carts, online orders and reservations, delivery areas/fees, cancellation, customer notifications, loyalty points, complaints, picking, delivery/pickup, failed-delivery returns, disputes and uncollected pickup orders.  
**Depends on:** Approved BRS Units 08–09 and 11, WEB-001–WEB-026, DLV-001–DLV-019, CRM-CUS/LOY/CMP requirements, P31–P40, ADR/ORD/DLV decisions, TD-03, TD-04, TD-05, TD-09, TD-13, TD-14, TD-19, TD-20 and TD-21.

> This is a logical/physical table specification for review, not executable DDL, EF Core code or a migration. It does not add saved customer addresses, guest checkout, order splitting, customer-selected delivery time, automatic product substitution, a second delivery attempt, or automatic refund on dispute.

## 1. Shared rules and ownership

1. Every PK is sequential application-generated `Guid` / SQL Server `uniqueidentifier`; no integer identity PK.
2. Central owns customer identity/profile, online order lifecycle, delivery-area/fee policies, loyalty rules/ledger, complaint management, fulfillment assignments, delivery disputes and pickup non-collection decisions.
3. Branch stores only the minimum assigned-order/customer data needed to execute its own picking, handover and delivery work. Branch/delivery employees cannot search customer profiles.
4. One online order is assigned to exactly one branch. No split fulfillment across branches.
5. An online order cannot be confirmed unless the selected branch is online/available and all requested products/quantities are available.
6. Cart contents and branch may change only before order confirmation/reservation. After confirmation, the customer cannot edit order contents or branch.
7. Central owns the 15-minute online reservation/payment lifecycle. Branch stock reservation acceptance is authoritative for physical stock exclusion.
8. Payment status, order status, fulfillment status, delivery attempt, dispute and refund status are independent.
9. No order may be restored after the 15-minute reservation expires and it is cancelled. A late payment is linked to the original cancelled order, does not reserve stock or start preparation, and follows the approved default refund/Central notification handling.
10. Customer notification channels for order status are website and email only; no SMS. Failed notification delivery does not change order/payment state.
11. Customer delivery disputes have no submission deadline, preserve the original delivery confirmation and require a Central decision; submission alone does not trigger a refund.
12. Loyalty points are credited only after completed sale and confirmed payment; earned points for returned items are reversed proportionally under approved rules; redemption occurs after discounts.
13. All cross-site references are GUID correlations with no cross-database FK.

## 2. Customer profiles, OTP verification and duplicate resolution

### 2.1 `crm.Customers` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CustomerId | uniqueidentifier | No | PK; sequential Guid |
| CustomerNumber | nvarchar(40) | No | Unique stable customer reference |
| FullName | nvarchar(200) | No | Customer display name |
| NormalizedPhone | nvarchar(32) | No | Unique normalized mobile number |
| PhoneVerifiedAtUtc | datetime2(3) | Yes | UTC; required before account activation |
| Email | nvarchar(254) | Yes | Optional email |
| CustomerStatus | tinyint | No | PendingVerification / Active / Suspended / Merged |
| CreatedAtUtc | datetime2(3) | No | UTC |
| UpdatedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique CustomerNumber and unique NormalizedPhone.
- CustomerStatus cannot be Active until PhoneVerifiedAtUtc is populated.
- Customer phone is the unique customer identifier. Existing duplicates are resolved centrally; no duplicate new account is allowed.
- No saved address columns/table are introduced. Delivery address is stored per order.
- Do not duplicate passwords or authentication tokens in Customers; customer authentication remains in the identity subsystem.

### 2.2 `crm.CustomerPhoneVerifications` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CustomerPhoneVerificationId | uniqueidentifier | No | PK; sequential Guid |
| CustomerId | uniqueidentifier | Yes | FK → Customers; nullable only before customer row is finalized |
| PhoneNumber | nvarchar(32) | No | Normalized target phone |
| OtpHash | varbinary(64) | No | One-way verifier; never plaintext OTP |
| ProviderReference | nvarchar(150) | Yes | OTP delivery provider reference |
| IssuedAtUtc | datetime2(3) | No | UTC |
| ExpiresAtUtc | datetime2(3) | No | UTC |
| VerifiedAtUtc | datetime2(3) | Yes | UTC |
| AttemptCount | int | No | Non-negative |
| VerificationStatus | tinyint | No | Pending / Verified / Expired / Failed / Locked |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Index PhoneNumber + VerificationStatus + ExpiresAtUtc.
- OTP hash only; no plaintext OTP or reusable secret.
- Verification attempt/rate limits are enforced by the security policy and are not invented by this schema.
- Customer account becomes Active only after successful verification of the same normalized phone number.

### 2.3 `crm.CustomerDuplicateResolutions` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CustomerDuplicateResolutionId | uniqueidentifier | No | PK; sequential Guid |
| DuplicateCustomerId | uniqueidentifier | No | FK → Customers |
| CanonicalCustomerId | uniqueidentifier | No | FK → Customers |
| ResolutionStatus | tinyint | No | Proposed / Approved / Rejected / Completed |
| Reason | nvarchar(1000) | No | Central review reason |
| DecidedByUserId | uniqueidentifier | Yes | Central actor |
| DecidedAtUtc | datetime2(3) | Yes | UTC |
| CompletedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- DuplicateCustomerId must differ from CanonicalCustomerId.
- A duplicate customer can be merged into only one canonical customer; enforce unique DuplicateCustomerId and prohibit merge cycles.
- No physical deletion or destructive rewrite of historical order/payment/loyalty references. A resolution preserves both identities and records a canonical lookup/merge mapping.
- Central-only operation; all affected customer profile/loyalty links are updated through an audited, transaction-safe process.

## 3. Online carts and orders

### 3.1 `online.Carts` and `CartLines` — Central

**`Carts`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CartId | uniqueidentifier | No | PK; sequential Guid |
| CustomerId | uniqueidentifier | No | FK → crm.Customers |
| SelectedBranchId | uniqueidentifier | Yes | FK → org.Branches; selected branch before confirmation |
| CartStatus | tinyint | No | Active / ConvertedToOrder / Abandoned |
| CreatedAtUtc | datetime2(3) | No | UTC |
| UpdatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**`CartLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CartLineId | uniqueidentifier | No | PK; sequential Guid |
| CartId | uniqueidentifier | No | FK → Carts |
| ProductId | uniqueidentifier | No | Central Product GUID |
| ProductUnitId | uniqueidentifier | No | Central ProductUnit GUID |
| Quantity | decimal(18,3) | No | Positive; unit precision validated |
| AddedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- Cart → Lines is 1:N.
- Customer must be Active and phone-verified to checkout.
- Before confirmation, customer may change items/quantities/selected branch. Every relevant change revalidates stock, branch availability, prices, promotions, delivery fee and minimum order amount.
- Cart price fields are not authoritative snapshots; checkout recalculates using current approved rules.

### 3.2 `online.Orders` and `OrderLines` — Central authoritative; Branch assigned-order projection

**`Orders`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| OrderId | uniqueidentifier | No | PK; sequential Guid |
| OrderNumber | nvarchar(50) | No | Unique human-readable reference |
| CustomerId | uniqueidentifier | No | FK → crm.Customers |
| BranchId | uniqueidentifier | No | FK → org.Branches; exactly one fulfillment branch |
| SourceType | tinyint | No | OnlineStore / BranchCreatedPaidPickup |
| FulfillmentType | tinyint | No | Delivery / BranchPickup |
| OrderStatus | tinyint | No | PendingPayment / Confirmed / Preparing / Ready / OutForDelivery / Delivered / PickupCompleted / Cancelled / Disputed / Closed |
| CurrencyCode | char(3) | No | JOD for current project |
| SubtotalAmount | decimal(18,3) | No | Pre-discount snapshot |
| DiscountAmount | decimal(18,3) | No | Promotion/coupon discounts |
| DeliveryFeeAmount | decimal(18,3) | No | Snapshot; zero for approved free delivery/pickup |
| LoyaltyRedemptionAmount | decimal(18,3) | No | Points value applied after discounts |
| FinalAmount | decimal(18,3) | No | Final order amount |
| ConfirmedAtUtc | datetime2(3) | Yes | UTC |
| CreatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**`OrderLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| OrderLineId | uniqueidentifier | No | PK; sequential Guid |
| OrderId | uniqueidentifier | No | FK → Orders |
| ProductId | uniqueidentifier | No | Product GUID |
| ProductUnitId | uniqueidentifier | No | Unit GUID |
| SKU | nvarchar(60) | No | Historical SKU snapshot |
| ProductName | nvarchar(200) | No | Historical product-name snapshot |
| Quantity | decimal(18,3) | No | Confirmed quantity |
| UnitPrice | decimal(18,3) | No | Approved unit-price snapshot |
| GrossLineAmount | decimal(18,3) | No | Pre-discount line total |
| DiscountAmount | decimal(18,3) | No | Product-level discount snapshot |
| NetLineAmount | decimal(18,3) | No | Line total after product discounts |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique OrderNumber.
- Order → Lines is 1:N; order must contain at least one line.
- One BranchId per order; no split orders.
- Customer and Branch FKs are Central-local.
- Order lines preserve product/unit/name/price/discount snapshots.
- After confirmation, customer cannot change order contents or selected Branch.
- Order status is independent of PaymentTransaction and Refund status.
- Branch projection contains only assigned order data and minimum customer delivery information required for fulfillment.

### 3.3 `online.OrderStatusHistory` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| OrderStatusHistoryId | uniqueidentifier | No | PK; sequential Guid |
| OrderId | uniqueidentifier | No | FK → Orders |
| PreviousStatus | tinyint | Yes | Null for initial status |
| NewStatus | tinyint | No | New order status |
| ChangedByUserId | uniqueidentifier | Yes | System/customer/employee actor GUID |
| ChangedAtUtc | datetime2(3) | No | UTC |
| ReasonCode | nvarchar(50) | Yes | Stable reason code |
| Reason | nvarchar(1000) | Yes | Explanation |
| CorrelationId | uniqueidentifier | No | Workflow correlation |

Status transitions are append-only. Payment status is never encoded as an OrderStatus value.

## 4. Reservation, delivery location, fees and store policies

### 4.1 `online.OrderReservations` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| OrderReservationId | uniqueidentifier | No | PK; sequential Guid |
| OrderId | uniqueidentifier | No | FK → Orders |
| ReservationStatus | tinyint | No | Pending / Accepted / PaymentConfirmed / Released / Expired / Rejected |
| RequestedAtUtc | datetime2(3) | No | UTC |
| ExpiresAtUtc | datetime2(3) | No | RequestedAtUtc + approved 15-minute window |
| PaymentConfirmedAtUtc | datetime2(3) | Yes | UTC |
| ReleasedAtUtc | datetime2(3) | Yes | UTC |
| ReleaseReason | tinyint | Yes | PaymentExpired / Cancelled / FulfillmentConsumed / CentralDecision |
| BranchStockReservationId | uniqueidentifier | Yes | Branch stock reservation GUID; no cross-db FK |
| OperationId | uniqueidentifier | No | Idempotency key |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- Order → OrderReservation is 1:1; unique OrderId prevents duplicate reservations for the same checkout. Reservation lifecycle changes are retained in OrderStatusHistory and synchronization events.
- The 15-minute deadline is centrally calculated. Branch does not independently extend the deadline.
- If payment is not confirmed by expiry, Central cancels the order and sends an idempotent release command.
- If payment is confirmed in time, the order proceeds to fulfillment; the 15-minute deadline no longer cancels it.
- Late payment is recorded against the original cancelled order, does not restore it or re-reserve stock, and follows the approved refund/Central follow-up path.
- Branch stock reservation acceptance is authoritative for physical stock exclusion; GUID correlation only, no cross-database FK.

### 4.2 `online.OrderDeliveryDetails` — Central; minimal assigned projection to Branch

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| OrderDeliveryDetailId | uniqueidentifier | No | PK; sequential Guid |
| OrderId | uniqueidentifier | No | Unique FK → Orders |
| DeliveryCityId | uniqueidentifier | Yes | FK → DeliveryCities; required for delivery |
| DeliveryAreaId | uniqueidentifier | Yes | FK → DeliveryAreas; required for delivery |
| AddressDetails | nvarchar(1000) | Yes | Free text street/building/apartment/landmark; required for delivery |
| CustomerContactName | nvarchar(150) | Yes | Minimal fulfillment contact |
| CustomerContactPhone | nvarchar(32) | Yes | Minimal fulfillment contact |
| DeliveryInstructions | nvarchar(500) | Yes | Optional instructions |
| RowVersion | rowversion | No | Concurrency token |

No reusable customer address table is created. The customer selects city/area and enters address details for every order. No customer-selected delivery date or time window exists.

### 4.3 `online.DeliveryCities`, `DeliveryAreas`, `DeliveryFeeRules` and `FreeDeliveryRules` — Central

**`DeliveryCities`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| DeliveryCityId | uniqueidentifier | No | PK; sequential Guid |
| CityCode | nvarchar(40) | No | Unique code |
| Name | nvarchar(120) | No | City display name |
| IsActive | bit | No | Availability |
| DisplayOrder | int | No | Non-negative |

**`DeliveryAreas`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| DeliveryAreaId | uniqueidentifier | No | PK; sequential Guid |
| DeliveryCityId | uniqueidentifier | No | FK → DeliveryCities |
| AreaCode | nvarchar(50) | No | Unique within city |
| Name | nvarchar(120) | No | Village/area display name |
| IsActive | bit | No | Availability |
| DisplayOrder | int | No | Non-negative |

**`DeliveryFeeRules`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| DeliveryFeeRuleId | uniqueidentifier | No | PK; sequential Guid |
| DeliveryAreaId | uniqueidentifier | No | FK → DeliveryAreas |
| FeeAmount | decimal(18,3) | No | Non-negative delivery fee |
| CurrencyCode | char(3) | No | JOD |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| IsActive | bit | No | Current rule |
| RowVersion | rowversion | No | Concurrency token |

**`FreeDeliveryRules`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| FreeDeliveryRuleId | uniqueidentifier | No | PK; sequential Guid |
| RuleCode | nvarchar(60) | No | Unique code |
| DeliveryAreaId | uniqueidentifier | Yes | Null = all areas; otherwise FK → DeliveryAreas |
| ConditionType | tinyint | No | Central-approved eligibility condition |
| ConditionPayload | nvarchar(max) | No | Structured condition such as campaign/order condition; schema-validated |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | No | UTC |
| IsActive | bit | No | Current rule |
| CreatedByUserId | uniqueidentifier | No | Central actor |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- DeliveryCity → DeliveryAreas is 1:N.
- DeliveryArea → DeliveryFeeRules is 1:N effective-dated history.
- FreeDeliveryRules are centrally defined and may target one area or all areas.
- Exactly one effective delivery fee applies to the selected area unless a valid free-delivery rule makes the fee zero.
- Free delivery is centrally controlled; the website does not invent free-delivery conditions.

### 4.4 `online.StorePolicies` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StorePolicyId | uniqueidentifier | No | PK; sequential Guid |
| Version | bigint | No | Monotonic Central version |
| MinimumOrderAmount | decimal(18,3) | No | Uniform minimum across all areas |
| CurrencyCode | char(3) | No | JOD |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| IsActive | bit | No | Current approved policy |
| ApprovedByUserId | uniqueidentifier | No | Central actor |
| ApprovedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

Minimum order value is uniform across cities/areas. Confirmation is blocked below the effective centrally approved amount.

## 5. Order cancellation and fulfillment exceptions

### 5.1 `online.OrderCancellationRequests`, `OrderCancellationDecisions` and `OrderCancellationDecisionLines`

**`OrderCancellationRequests`** — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| OrderCancellationRequestId | uniqueidentifier | No | PK; sequential Guid |
| OrderId | uniqueidentifier | No | FK → Orders |
| RequestedByCustomerId | uniqueidentifier | Yes | Customer GUID if customer initiated |
| RequestedByUserId | uniqueidentifier | Yes | Employee/system actor if internally initiated |
| RequestedAtUtc | datetime2(3) | No | UTC |
| CancellationStage | tinyint | No | BeforePreparation / AfterPreparationStarted |
| Reason | nvarchar(1000) | No | Required reason |
| RequestStatus | tinyint | No | Submitted / Approved / Rejected |
| RowVersion | rowversion | No | Concurrency token |

**`OrderCancellationDecisions`** — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| OrderCancellationDecisionId | uniqueidentifier | No | PK; sequential Guid |
| OrderCancellationRequestId | uniqueidentifier | No | FK → OrderCancellationRequests |
| Decision | tinyint | No | Approve / Reject |
| Reason | nvarchar(1000) | No | Central decision reason |
| DecidedByUserId | uniqueidentifier | No | Central actor |
| DecidedAtUtc | datetime2(3) | No | UTC |

**`OrderCancellationDecisionLines`** — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| OrderCancellationDecisionLineId | uniqueidentifier | No | PK; sequential Guid |
| OrderCancellationDecisionId | uniqueidentifier | No | FK → OrderCancellationDecisions |
| OrderLineId | uniqueidentifier | No | FK → OrderLines |
| ProductDisposition | tinyint | No | AwaitBranchReturnInspection / ReturnToSellableAfterInspection / Quarantine / OtherApprovedDisposition |
| Quantity | decimal(18,3) | No | Affected quantity |
| Reason | nvarchar(500) | Yes | Line-specific reason |

Cancellation before preparation requires Central approval. Cancellation after preparation begins also requires a Central decision for each affected product's disposition. Products are not automatically returned to sellable stock.

### 5.2 `fulfillment.FulfillmentTasks`, `FulfillmentExceptions`, `FulfillmentExceptionDecisions` and lines

**`FulfillmentTasks`** — Central assignment; Branch assigned-work projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| FulfillmentTaskId | uniqueidentifier | No | PK; sequential Guid |
| OrderId | uniqueidentifier | No | FK → Orders |
| BranchId | uniqueidentifier | No | FK → org.Branches |
| TaskType | tinyint | No | Picking / Delivery / Pickup |
| TaskStatus | tinyint | No | Assigned / InProgress / Blocked / Completed / Cancelled |
| AssignedAtUtc | datetime2(3) | No | UTC |
| CompletedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

**`FulfillmentExceptions`** — Branch fact; Central review copy

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| FulfillmentExceptionId | uniqueidentifier | No | PK; generated at Branch and preserved centrally |
| FulfillmentTaskId | uniqueidentifier | No | Task GUID |
| OrderLineId | uniqueidentifier | No | Central order line GUID |
| ExceptionType | tinyint | No | Unavailable / Damaged / BarcodeMismatch / Other |
| AffectedQuantity | decimal(18,3) | No | Positive quantity |
| ReportedByEmployeeId | uniqueidentifier | No | Branch employee |
| ReportedAtUtc | datetime2(3) | No | UTC |
| Description | nvarchar(1000) | Yes | Details |
| ExceptionStatus | tinyint | No | Submitted / UnderCentralReview / Resolved |

**`FulfillmentExceptionDecisions`** — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| FulfillmentExceptionDecisionId | uniqueidentifier | No | PK; sequential Guid |
| FulfillmentExceptionId | uniqueidentifier | No | Central exception GUID |
| Decision | tinyint | No | Central-approved outcome code |
| Reason | nvarchar(1000) | No | Required |
| DecidedByUserId | uniqueidentifier | No | Central actor |
| DecidedAtUtc | datetime2(3) | No | UTC |

**`FulfillmentExceptionDecisionLines`** — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| FulfillmentExceptionDecisionLineId | uniqueidentifier | No | PK; sequential Guid |
| FulfillmentExceptionDecisionId | uniqueidentifier | No | FK → FulfillmentExceptionDecisions |
| OrderLineId | uniqueidentifier | No | FK → Central OrderLines |
| ApprovedOutcome | tinyint | No | Central decision outcome; no substitute outcome is permitted |
| Quantity | decimal(18,3) | No | Affected quantity |
| Reason | nvarchar(500) | Yes | Line-specific explanation |

**Rules**
- Branch stops preparation of the affected order/line and refers the issue to Central.
- Neither Branch nor system proposes or applies a substitute.
- Central decision is sent to Branch as an authenticated command; Branch executes only the approved outcome and reports acknowledgement.
- Order status remains separate from payment/refund status.

## 6. Customer notifications

### 6.1 `online.OrderNotifications` and `NotificationDeliveryAttempts` — Central

**`OrderNotifications`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| OrderNotificationId | uniqueidentifier | No | PK; sequential Guid |
| OrderId | uniqueidentifier | No | FK → Orders |
| CustomerId | uniqueidentifier | No | FK → Customers |
| NotificationType | tinyint | No | Order status event type |
| Channel | tinyint | No | Website / Email only |
| Subject | nvarchar(200) | Yes | Email subject where applicable |
| MessageReference | nvarchar(300) | No | Template/content reference |
| CreatedAtUtc | datetime2(3) | No | UTC |
| NotificationStatus | tinyint | No | Pending / Sent / Failed |

**`NotificationDeliveryAttempts`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| NotificationDeliveryAttemptId | uniqueidentifier | No | PK; sequential Guid |
| OrderNotificationId | uniqueidentifier | No | FK → OrderNotifications |
| AttemptNumber | int | No | Positive sequence |
| AttemptedAtUtc | datetime2(3) | No | UTC |
| DeliveryStatus | tinyint | No | Sent / Failed |
| ProviderReference | nvarchar(150) | Yes | Email provider reference |
| FailureReason | nvarchar(500) | Yes | Sanitized failure |

**Constraints**
- Order → notifications is 1:N; notification → attempts is 1:N.
- Channel allowlist is Website and Email. SMS is prohibited for customer order-status notifications.
- Delivery failure does not change OrderStatus or PaymentTransaction status.

## 7. Customer loyalty and points

### 7.1 `crm.LoyaltyProgramRules` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| LoyaltyProgramRuleId | uniqueidentifier | No | PK; sequential Guid |
| Version | bigint | No | Monotonic version |
| EarningRulePayload | nvarchar(max) | No | Centrally configured earn calculation and eligible-spend basis |
| RedemptionRulePayload | nvarchar(max) | No | Centrally configured redemption value, minimum and limits |
| PointsValidityDays | int | No | Centrally defined validity period |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| IsActive | bit | No | Current rule |
| ApprovedByUserId | uniqueidentifier | No | Central actor |
| ApprovedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- Unique Version; PointsValidityDays > 0.
- Structured rule payloads are schema-validated and versioned; do not hardcode earning/redemption rates in invoice code.
- Rule version applied is snapshotted in every loyalty transaction.
- Points apply after discounts and are usable at Branch POS and Online Store under central rules.

### 7.2 `crm.LoyaltyAccounts` — Central; restricted Branch projection where required

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| LoyaltyAccountId | uniqueidentifier | No | PK; sequential Guid |
| CustomerId | uniqueidentifier | No | Unique FK → Customers |
| AccountStatus | tinyint | No | Active / Suspended |
| CurrentPointsBalance | decimal(18,3) | No | Cached projection; ledger remains source of truth |
| UpdatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- Unique CustomerId; one loyalty account per customer.
- Balance is reconciled from the immutable point ledger and lot allocations; never treat it as independent authority.

### 7.3 `crm.LoyaltyPointTransactions` — Central authoritative; Branch durable event for approved Offline POS

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| LoyaltyPointTransactionId | uniqueidentifier | No | PK; generated at origin and preserved centrally |
| LoyaltyAccountId | uniqueidentifier | No | Loyalty account GUID |
| TransactionType | tinyint | No | Earn / Redeem / Expire / EarnReversal / RedeemReversal / Adjustment |
| PointsDelta | decimal(18,3) | No | Signed points change |
| MonetaryValue | decimal(18,3) | Yes | Monetary value for redemption/adjustment where applicable |
| SourceType | tinyint | No | POSInvoice / OnlineOrder / SalesReturn / Expiry / CentralAdjustment |
| SourceTransactionId | uniqueidentifier | No | Invoice/order/return GUID |
| RuleVersion | bigint | No | Loyalty rule version applied |
| OccurredAtUtc | datetime2(3) | No | UTC |
| OperationId | uniqueidentifier | No | Idempotency key |
| Reason | nvarchar(500) | Yes | Required for adjustment/reversal exception |
| RowVersion | rowversion | No | Concurrency token |

### 7.4 `crm.LoyaltyPointLots` and `LoyaltyPointLotAllocations` — Central

**`LoyaltyPointLots`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| LoyaltyPointLotId | uniqueidentifier | No | PK; sequential Guid |
| EarnTransactionId | uniqueidentifier | No | Unique FK → earning LoyaltyPointTransactions |
| LoyaltyAccountId | uniqueidentifier | No | FK → LoyaltyAccounts |
| EarnedPoints | decimal(18,3) | No | Positive earned points |
| RemainingPoints | decimal(18,3) | No | Current lot balance projection |
| EarnedAtUtc | datetime2(3) | No | UTC |
| ExpiresAtUtc | datetime2(3) | No | UTC expiry from applied rule |
| LotStatus | tinyint | No | Active / PartiallyConsumed / Exhausted / Expired / Reversed |
| RowVersion | rowversion | No | Concurrency token |

**`LoyaltyPointLotAllocations`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| LoyaltyPointLotAllocationId | uniqueidentifier | No | PK; sequential Guid |
| LoyaltyPointLotId | uniqueidentifier | No | FK → LoyaltyPointLots |
| LoyaltyPointTransactionId | uniqueidentifier | No | FK → LoyaltyPointTransactions |
| AllocatedPoints | decimal(18,3) | No | Positive allocated amount |
| AllocationType | tinyint | No | Redeem / Restore / Reverse |
| AllocatedAtUtc | datetime2(3) | No | UTC |
| OperationId | uniqueidentifier | No | Idempotency key |

**Constraints and rules**
- Customer → LoyaltyAccount is 1:0..1.
- Account → PointTransactions is 1:N.
- Earn transaction creates one point lot with expiry based on the rule version used.
- Redeem transaction allocates against one or more non-expired lots; expired lots cannot be redeemed.
- Expiry posts an immutable Expire transaction and consumes the remaining points in the lot.
- Return of goods creates an EarnReversal proportional to the returned value under the approved rules. Previously redeemed points are handled according to the approved refund/return policy and recorded as separate RedeemReversal/adjustment transactions; no ledger row is rewritten.
- Points are credited only after completed sale and trusted payment confirmation.
- Offline Branch point events are durably recorded and reconciled idempotently; Central remains the loyalty authority.

## 8. Customer complaints

### 8.1 `crm.CustomerComplaints` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CustomerComplaintId | uniqueidentifier | No | PK; sequential Guid |
| ComplaintNumber | nvarchar(50) | No | Unique reference number |
| CustomerId | uniqueidentifier | No | FK → Customers |
| RelatedOrderId | uniqueidentifier | Yes | Optional FK → online.Orders |
| ComplaintCategory | tinyint | No | Stable category code |
| Description | nvarchar(2000) | No | Complaint details |
| ComplaintStatus | tinyint | No | New / Assigned / InProgress / WaitingCustomer / Resolved / Closed |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor |
| ClosedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

### 8.2 `crm.ComplaintAssignments` and `ComplaintActions` — Central

**`ComplaintAssignments`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ComplaintAssignmentId | uniqueidentifier | No | PK; sequential Guid |
| CustomerComplaintId | uniqueidentifier | No | FK → CustomerComplaints |
| AssignedEmployeeId | uniqueidentifier | No | Central responsible employee |
| AssignedByUserId | uniqueidentifier | No | Central actor |
| AssignedAtUtc | datetime2(3) | No | UTC |
| UnassignedAtUtc | datetime2(3) | Yes | UTC |
| AssignmentReason | nvarchar(500) | Yes | Optional |

**`ComplaintActions`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ComplaintActionId | uniqueidentifier | No | PK; sequential Guid |
| CustomerComplaintId | uniqueidentifier | No | FK → CustomerComplaints |
| ComplaintAssignmentId | uniqueidentifier | Yes | FK → active/relevant assignment |
| ActionType | tinyint | No | Note / StatusChanged / Contacted / Resolved / Reopened / Closed |
| ActionDescription | nvarchar(2000) | No | Action/note |
| PreviousStatus | tinyint | Yes | Previous status |
| NewStatus | tinyint | Yes | New status |
| ActorUserId | uniqueidentifier | No | Central actor |
| OccurredAtUtc | datetime2(3) | No | UTC |

**Constraints**
- ComplaintNumber unique.
- Complaint → Assignments and Actions are 1:N.
- Current responsible employee is the latest active assignment; assignment history remains preserved.
- Complaint management is Central-only; Branch employees cannot browse customer profiles or complaints.

## 9. Picking and order preparation

### 9.1 `fulfillment.PickingSessions` and `PickingLines` — Branch

**`PickingSessions`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PickingSessionId | uniqueidentifier | No | PK; sequential Guid |
| FulfillmentTaskId | uniqueidentifier | No | Central task GUID; no cross-db FK |
| OrderId | uniqueidentifier | No | Central order GUID; no cross-db FK |
| PickedByEmployeeId | uniqueidentifier | No | Branch employee |
| StartedAtUtc | datetime2(3) | No | UTC |
| CompletedAtUtc | datetime2(3) | Yes | UTC |
| SessionStatus | tinyint | No | Started / InProgress / Blocked / Completed |
| RowVersion | rowversion | No | Concurrency token |

**`PickingLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PickingLineId | uniqueidentifier | No | PK; sequential Guid |
| PickingSessionId | uniqueidentifier | No | FK → PickingSessions |
| OrderLineId | uniqueidentifier | No | Central order line GUID |
| OrderedProductId | uniqueidentifier | No | Ordered product GUID |
| OrderedQuantity | decimal(18,3) | No | Ordered quantity |
| PickedProductId | uniqueidentifier | Yes | Actual scanned product GUID; only equal match can be accepted |
| PickedQuantity | decimal(18,3) | No | Actual picked quantity |
| LineStatus | tinyint | No | Pending / Matched / Mismatch / Unavailable / Completed |
| RowVersion | rowversion | No | Concurrency token |

### 9.2 `fulfillment.PickingLineScans` — Branch

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PickingLineScanId | uniqueidentifier | No | PK; sequential Guid |
| PickingLineId | uniqueidentifier | No | FK → PickingLines |
| ScannedBarcode | nvarchar(64) | No | Exact scanned value |
| ResolvedProductId | uniqueidentifier | Yes | Product identified by barcode |
| ScanResult | tinyint | No | Match / Mismatch / UnknownBarcode |
| ScannedByEmployeeId | uniqueidentifier | No | Branch employee |
| ScannedAtUtc | datetime2(3) | No | UTC |

**Constraints**
- PickingSession → Lines → Scans is 1:N.
- A mismatch or unknown barcode is rejected and cannot be recorded as the ordered product.
- Ordered and actual picked product/quantity remain visible for Central review.
- No substitute product is selected automatically or by Branch employee.

## 10. Delivery shift coordination, assignment and single attempt

### 10.1 `fulfillment.DeliveryShiftCoordinators` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| DeliveryShiftCoordinatorId | uniqueidentifier | No | PK; sequential Guid |
| BranchId | uniqueidentifier | No | FK → org.Branches |
| ShiftDate | date | No | Branch-local shift date |
| ShiftCode | nvarchar(30) | No | Stable shift identifier |
| CoordinatorEmployeeId | uniqueidentifier | No | Central-appointed employee |
| AppointedByUserId | uniqueidentifier | No | Central actor |
| AppointedAtUtc | datetime2(3) | No | UTC |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- Unique BranchId + ShiftDate + ShiftCode + EffectiveFromUtc.
- Only Central appoints/changes the coordinator. Coordinator may assign delivery tasks but cannot administer users, prices, inventory, policies or permissions.

### 10.2 `fulfillment.DeliveryAssignments` — Central; assigned-task projection to Branch

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| DeliveryAssignmentId | uniqueidentifier | No | PK; sequential Guid |
| FulfillmentTaskId | uniqueidentifier | No | FK → Central FulfillmentTasks |
| DeliveryShiftCoordinatorId | uniqueidentifier | No | FK → DeliveryShiftCoordinators |
| DeliveryEmployeeId | uniqueidentifier | No | Assigned employee GUID |
| AssignedByCoordinatorId | uniqueidentifier | No | Coordinator GUID |
| AssignedAtUtc | datetime2(3) | No | UTC |
| AssignmentStatus | tinyint | No | Assigned / Accepted / InProgress / Completed / Cancelled |
| RowVersion | rowversion | No | Concurrency token |

### 10.3 `fulfillment.DeliveryAttempts` — Branch execution; Central synchronized

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| DeliveryAttemptId | uniqueidentifier | No | PK; sequential Guid |
| OrderId | uniqueidentifier | No | Central order GUID |
| DeliveryAssignmentId | uniqueidentifier | No | Central assignment GUID |
| AttemptNumber | tinyint | No | Must equal 1 |
| AttemptStatus | tinyint | No | Delivered / Failed |
| AttemptedAtUtc | datetime2(3) | No | UTC |
| RecordedByEmployeeId | uniqueidentifier | No | Delivery employee |
| FailureReason | nvarchar(1000) | Yes | Required if failed |
| OperationId | uniqueidentifier | No | Idempotency key |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- Unique OrderId; exactly one delivery attempt per order.
- AttemptNumber check = 1. Technical retries use the same OperationId and cannot create a second customer attempt.
- A failed attempt triggers return-to-branch workflow; no further delivery attempt is scheduled.

### 10.4 `fulfillment.DeliveryProofs` — Branch execution; Central reference

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| DeliveryProofId | uniqueidentifier | No | PK; sequential Guid |
| DeliveryAttemptId | uniqueidentifier | No | FK → local DeliveryAttempts |
| ConfirmedByEmployeeId | uniqueidentifier | No | Delivery employee |
| ConfirmedAtUtc | datetime2(3) | No | UTC |
| ConfirmationNotes | nvarchar(1000) | Yes | Optional manual notes |
| EvidenceReference | nvarchar(500) | Yes | Optional protected evidence reference |

Delivery confirmation is manual. No OTP or electronic signature is required.

## 11. Pickup handover, failed delivery returns and disputes

### 11.1 `fulfillment.PickupHandovers` — Branch execution; Central synchronized

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PickupHandoverId | uniqueidentifier | No | PK; sequential Guid |
| FulfillmentTaskId | uniqueidentifier | No | Central task GUID |
| OrderId | uniqueidentifier | No | Central order GUID |
| HandedOverByEmployeeId | uniqueidentifier | No | Branch employee |
| CustomerIdentityVerified | bit | No | Must be true before handover |
| VerificationMethod | tinyint | No | ManualDocumentOrIdentityCheck; no OTP/e-signature |
| HandedOverAtUtc | datetime2(3) | No | UTC |
| HandoverStatus | tinyint | No | Completed / Rejected |
| Notes | nvarchar(500) | Yes | Optional |

### 11.2 `fulfillment.FailedDeliveryReturns`, `FailedDeliveryReturnLines` and `DeliveryReturnInspections` — Branch

**`FailedDeliveryReturns`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| FailedDeliveryReturnId | uniqueidentifier | No | PK; sequential Guid |
| DeliveryAttemptId | uniqueidentifier | No | FK → local DeliveryAttempts |
| OrderId | uniqueidentifier | No | Central order GUID |
| ReceivedByEmployeeId | uniqueidentifier | No | Branch employee |
| ReceivedAtUtc | datetime2(3) | No | UTC |
| ReturnStatus | tinyint | No | Expected / Received / InspectionPending / Inspected / Closed |
| RowVersion | rowversion | No | Concurrency token |

**`FailedDeliveryReturnLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| FailedDeliveryReturnLineId | uniqueidentifier | No | PK; sequential Guid |
| FailedDeliveryReturnId | uniqueidentifier | No | FK → FailedDeliveryReturns |
| OrderLineId | uniqueidentifier | No | Central order line GUID |
| ProductId | uniqueidentifier | No | Product GUID |
| ProductUnitId | uniqueidentifier | No | Unit GUID |
| ReturnedQuantity | decimal(18,3) | No | Actual quantity returned to branch |

**`DeliveryReturnInspections`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| DeliveryReturnInspectionId | uniqueidentifier | No | PK; sequential Guid |
| FailedDeliveryReturnLineId | uniqueidentifier | No | FK → FailedDeliveryReturnLines |
| InspectionOutcome | tinyint | No | Sellable / QuarantineOrReview / DamagedOrExpired |
| InspectedByEmployeeId | uniqueidentifier | No | Authorized Branch inspector |
| InspectedAtUtc | datetime2(3) | No | UTC |
| Notes | nvarchar(1000) | Yes | Inspection details |
| InventoryMovementId | uniqueidentifier | Yes | Local stock movement when returned to sellable stock |
| InventoryQuarantineId | uniqueidentifier | Yes | Local quarantine when isolated |

**Rules**
- Exactly one failed delivery attempt is permitted.
- Goods return to the responsible Branch and enter pending inspection.
- No returned goods become sellable automatically. Inspection and Inventory disposition rules apply.

### 11.3 `fulfillment.DeliveryDisputes` and `DeliveryDisputeDecisions` — Central

**`DeliveryDisputes`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| DeliveryDisputeId | uniqueidentifier | No | PK; sequential Guid |
| OrderId | uniqueidentifier | No | FK → Orders |
| DeliveryAttemptId | uniqueidentifier | No | Delivery attempt GUID |
| CustomerId | uniqueidentifier | No | FK → Customers |
| DisputeDescription | nvarchar(2000) | No | Customer dispute |
| SubmittedAtUtc | datetime2(3) | No | UTC; no deadline |
| DisputeStatus | tinyint | No | Submitted / UnderReview / Accepted / Rejected / Closed |
| RowVersion | rowversion | No | Concurrency token |

**`DeliveryDisputeDecisions`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| DeliveryDisputeDecisionId | uniqueidentifier | No | PK; sequential Guid |
| DeliveryDisputeId | uniqueidentifier | No | FK → DeliveryDisputes |
| Decision | tinyint | No | Accept / Reject |
| Reason | nvarchar(1000) | No | Central decision reason |
| DecidedByUserId | uniqueidentifier | No | Central actor |
| DecidedAtUtc | datetime2(3) | No | UTC |

**Rules**
- No customer objection deadline is applied.
- Original delivery attempt, assignment, employee and timestamp remain unchanged.
- Dispute submission alone does not create a refund. Refund requires a separate approved Payments workflow.

## 12. Paid pickup orders not collected

### 12.1 `fulfillment.PickupRetentionPolicies` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PickupRetentionPolicyId | uniqueidentifier | No | PK; sequential Guid |
| Version | bigint | No | Monotonic policy version |
| RetentionDurationValue | int | No | Positive duration value |
| RetentionDurationUnit | tinyint | No | Hours / Days |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| IsActive | bit | No | Current policy |
| ApprovedByUserId | uniqueidentifier | No | Central actor |
| ApprovedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

### 12.2 `fulfillment.PickupNonCollectionCases` and `PickupNonCollectionDecisions` — Central

**`PickupNonCollectionCases`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PickupNonCollectionCaseId | uniqueidentifier | No | PK; sequential Guid |
| OrderId | uniqueidentifier | No | FK → Orders |
| PickupRetentionPolicyId | uniqueidentifier | No | FK → PickupRetentionPolicies |
| PaidAtUtc | datetime2(3) | No | UTC |
| RetentionExpiresAtUtc | datetime2(3) | No | Calculated from centrally approved policy |
| CaseStatus | tinyint | No | Waiting / AwaitingCentralDecision / Decided / Closed |
| RowVersion | rowversion | No | Concurrency token |

**`PickupNonCollectionDecisions`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PickupNonCollectionDecisionId | uniqueidentifier | No | PK; sequential Guid |
| PickupNonCollectionCaseId | uniqueidentifier | No | FK → PickupNonCollectionCases |
| Decision | tinyint | No | Central-approved outcome code |
| ProductDisposition | tinyint | No | AwaitInspection / ReturnToSellableAfterInspection / Quarantine / OtherApproved |
| Reason | nvarchar(1000) | No | Central decision reason |
| DecidedByUserId | uniqueidentifier | No | Central actor |
| DecidedAtUtc | datetime2(3) | No | UTC |

**Rules**
- Paid pickup orders created through a Branch are treated as completed sales if the customer does not attend.
- Central retention duration is applied; after it expires, Central decides the next action.
- Products are not automatically returned to stock. They must be physically inspected before any inventory disposition.

## 13. Requirement traceability

| Approved requirement/decision | Schema support |
|---|---|
| WEB-001–WEB-002, CRM-CUS-001–006, P31/P34 | Customers, CustomerPhoneVerifications, unique normalized phone, identity boundary and duplicate resolution |
| WEB-003–WEB-006, WEB-014–WEB-020 | Orders/lines, one Branch, Cart/checkout validation, OrderReservations and late-payment handling |
| WEB-007–WEB-009, PDI-01–PDI-04 | Catalog tables in TD-16; online order product projections |
| WEB-010–WEB-013, ADR-01–ADR-04, DLV-01–DLV-03, ORD-01–ORD-02 | Delivery cities/areas/details/fees/free-delivery rules and uniform StorePolicies minimum |
| WEB-021–WEB-024 | Cancellation requests/decisions/lines and separate Payments refund workflow |
| WEB-025 | OrderNotifications and NotificationDeliveryAttempts; Website/Email only |
| WEB-026 | Assigned-order projections and restricted minimum customer data |
| DLV-001–DLV-007 | FulfillmentTasks, PickingSessions/Lines/Scans, FulfillmentExceptions |
| DLV-008–DLV-013 | DeliveryShiftCoordinators, DeliveryAssignments, one DeliveryAttempt, manual proof |
| DLV-014–DLV-018 | FailedDeliveryReturns/Inspections, DeliveryDisputes and Central decisions |
| DLV-019 | PickupRetentionPolicies, PickupNonCollectionCases/Decisions |
| CRM-LOY-001–006, P32/P33/LOY-04 | Loyalty rules, accounts, immutable transactions, expiring lots and allocations |
| CRM-CMP-001–004, P35 | Complaint number, assignment history, action history and Central-only management |

## 14. Technical risks for review

1. Define the exact customer identity table mapping under TD-06. Customer credentials must remain separate from employee credentials.
2. Define OTP provider, delivery limits, expiry/attempt limits and secure hash parameters in the security design; do not hardcode them in the schema.
3. Define the exact central/branch order projection fields and privacy minimization; Branch must not have customer search access.
4. Define whether an order may contain both delivery and pickup lines (this proposal assumes one FulfillmentType per order).
5. Define free-delivery rule evaluation and precedence against other promotions; no duplicate discount may be applied.
6. Define late payment refund timing and Central notification workflow; late payment must never restore the cancelled order.
7. Define the exact order-level outcome codes for fulfillment exceptions and cancellation decisions; no substitute product is permitted.
8. Define how one delivery attempt is enforced atomically under concurrent/retried Branch operations.
9. Define customer dispute evidence retention and access control; no fixed dispute deadline exists.
10. Define point-lot consumption order, expiry processing schedule and the approved treatment of previously redeemed points on partial return.
11. Define reconciliation when a duplicate customer account has existing orders, loyalty points, complaints and identity credentials; preserve all source transaction references.
12. Define paid pickup retention duration values in the Central policy; this document does not invent a number.

## 15. Approval boundary and next step

TD-22 specifies Online Orders, Customer, Loyalty and Fulfillment only. It does not create SQL tables, EF Core entities, DDL or migrations. The next detailed group is Expenses, Payroll Tracking, Maintenance, Alerts, Audit and Reporting, followed by a full schema consistency review.

**No SQL table, EF Core entity, DDL or migration is created by this document.**
