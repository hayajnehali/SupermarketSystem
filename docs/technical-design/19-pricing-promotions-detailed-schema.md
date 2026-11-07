# TD-19 — Detailed Logical Schema: Pricing, Promotions and Coupons

**Version:** 0.1  
**Status:** Proposed for owner review  
**Date:** 2026-11-07  
**Scope:** Central base prices by product/selling unit, branch-specific approved exceptions, version publication, product/basket promotions, promotion conditions/rewards, coupon definitions and redemption history.  
**Depends on:** Approved BRS Unit 05, PRC-01–PRC-12, PRM-01–PRM-03, CUP-01–CUP-03, GAP-13/GAP-14, TD-03, TD-04, TD-05, TD-13 and TD-14.

> This is a logical/physical table specification for review, not executable DDL, EF Core source code or a migration. Central alone manages prices/promotions/coupons. Branches consume approved versioned snapshots and record local use events where Offline operation permits.

## 1. Pricing invariants

1. Every PK is client-generated sequential `Guid` / SQL Server `uniqueidentifier`; no integer identity PK.
2. Prices/discounts use `decimal`, not floating point. JOD final invoice amounts are rounded to two decimals; internal calculations preserve appropriate precision. The approved technical half-rounding rule remains to be fixed before implementation.
3. All price and promotion effective instants use `datetime2(3)` UTC.
4. A price is defined per Product + ProductUnit. Each approved selling unit can have its own price.
5. The default base price is uniform across branches. A branch-specific exception is possible only when Central explicitly approves it.
6. A new approved version creates new effective-dated rows; it does not rewrite completed invoice/order snapshots.
7. Branches cannot create, modify, approve, extend or deactivate central pricing/promotions/coupons.
8. Offline branches use the latest successfully applied approved snapshot. An expired promotion is not applied; the approved base price remains the fallback.
9. Before sale completion, the POS recalculates any cart affected by a price/promotion validity change, displays the change and requires cashier confirmation.
10. The cashier cannot override a base price, promotion result or coupon eligibility.
11. Promotion calculation follows the approved order: product-level promotions first; among eligible product promotions the best customer outcome is used unless Central explicitly permits stacking; basket promotions and coupons are not automatically combined; loyalty points are applied after discounts. The same discount must not be applied twice.
12. Promotion conditions, validity, customer/order usage limits and maximum benefit quantity are evaluated centrally or against the last approved local rules as permitted.

## 2. Placement

| Table | Central DB | Each Branch DB |
|---|---|---|
| `pricing.PriceVersions` | Authoritative | Approved version projection |
| `pricing.ProductPrices` | Authoritative | Read-only approved price projection |
| `pricing.BranchPricingSnapshots` | No | Local applied-version/hash record |
| `pricing.PromotionVersions` | Authoritative | Approved version projection |
| `pricing.Promotions` | Authoritative | Read-only approved promotion projection |
| `pricing.PromotionConditions` | Authoritative | Read-only approved projection |
| `pricing.PromotionRewards` | Authoritative | Read-only approved projection |
| `pricing.Coupons` | Authoritative | Read-only approved projection where permitted |
| `pricing.CouponRedemptions` | Central online authority and consolidated history | Durable local redemption event for permitted Offline POS use |
| `pricing.PromotionRedemptions` | Central online authority and consolidated history | Durable local promotion-usage event for POS Offline use |

A Branch redemption event and its Central synchronized copy share the same GUID. There is no cross-database FK. Central reconciles usage after synchronization; an Offline branch must not silently invent a newer central usage state.

## 3. Base prices and publication versions

### 3.1 `pricing.PriceVersions` — Central; approved projection to Branch

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PriceVersionId | uniqueidentifier | No | PK; sequential Guid |
| VersionNumber | bigint | No | Monotonic Central publication version |
| VersionStatus | tinyint | No | Draft / Approved / Published / Superseded |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor |
| ApprovedAtUtc | datetime2(3) | Yes | UTC |
| ApprovedByUserId | uniqueidentifier | Yes | Central approver |
| PublishedAtUtc | datetime2(3) | Yes | UTC |
| PayloadHash | binary(32) | Yes | SHA-256 of canonical publication payload |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique VersionNumber.
- Check VersionNumber > 0 and EffectiveToUtc is null or later than EffectiveFromUtc.
- Published versions are immutable; corrections create a new version.
- A Branch applies only a valid authenticated publication and records the applied version in BranchPricingSnapshots.

### 3.2 `pricing.ProductPrices` — Central; approved Branch projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductPriceId | uniqueidentifier | No | PK; sequential Guid |
| PriceVersionId | uniqueidentifier | No | FK → PriceVersions in Central; local FK to copied PriceVersions where projected |
| ProductId | uniqueidentifier | No | Central Product GUID; Branch local FK only if catalog projection exists |
| ProductUnitId | uniqueidentifier | No | Central ProductUnit GUID; Branch local FK only if catalog projection exists |
| BranchId | uniqueidentifier | Yes | Null = uniform company price; non-null = explicitly approved branch exception |
| CurrencyCode | char(3) | No | Currency code; JOD for current project |
| UnitPrice | decimal(18,3) | No | Price per selected selling unit |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| IsActive | bit | No | Current row status |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor |
| ApprovedByUserId | uniqueidentifier | No | Central approver |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Check UnitPrice >= 0 and EffectiveToUtc is null or later than EffectiveFromUtc.
- FK to PriceVersions; Product/ProductUnit references are Central-local FKs.
- Use separate filtered unique indexes for company-wide rows (BranchId IS NULL) and branch exception rows (BranchId IS NOT NULL), scoped by PriceVersionId + ProductId + ProductUnitId + BranchId.
- Only Central may create a branch-specific exception. The default remains company-wide uniform pricing.
- A published price row is immutable. A later change creates a new row/version.
- A price is always associated with an active approved ProductUnit; unit prices are not stored in Catalog.ProductUnits.

### 3.3 `pricing.BranchPricingSnapshots` — Branch

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| BranchPricingSnapshotId | uniqueidentifier | No | PK; sequential Guid |
| PriceVersionNumber | bigint | No | Last successfully applied price version |
| PromotionVersionNumber | bigint | No | Last successfully applied promotion/coupon publication version |
| AppliedAtUtc | datetime2(3) | No | UTC |
| PayloadHash | binary(32) | No | SHA-256 of applied snapshot |
| IsActive | bit | No | At most one active snapshot |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Filtered unique index allowing at most one active snapshot row.
- The active snapshot is advanced only after the full validated publication is durably applied.
- Branch cannot change version numbers, extend validity or create a local override.

## 4. Promotion publication and promotions

### 4.1 `pricing.PromotionVersions` — Central; approved projection to Branch

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PromotionVersionId | uniqueidentifier | No | PK; sequential Guid |
| VersionNumber | bigint | No | Monotonic Central publication version |
| VersionStatus | tinyint | No | Draft / Approved / Published / Superseded |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor |
| ApprovedAtUtc | datetime2(3) | Yes | UTC |
| ApprovedByUserId | uniqueidentifier | Yes | Central approver |
| PublishedAtUtc | datetime2(3) | Yes | UTC |
| PayloadHash | binary(32) | Yes | SHA-256 of canonical promotion/coupon publication |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique VersionNumber; VersionNumber > 0.
- Published versions are immutable; corrections create a new publication version.
- A Branch advances PromotionVersionNumber only after the complete promotion/coupon snapshot is validated and durably applied.

### 4.2 `pricing.Promotions` — Central; approved Branch projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PromotionId | uniqueidentifier | No | PK; sequential Guid |
| PromotionVersionId | uniqueidentifier | No | FK → PromotionVersions |
| PromotionCode | nvarchar(60) | No | Stable promotion code |
| Version | int | No | Positive version per PromotionCode |
| Name | nvarchar(160) | No | Display name |
| Description | nvarchar(1000) | Yes | Optional description |
| PromotionScope | tinyint | No | ProductLevel / BasketLevel |
| PromotionStatus | tinyint | No | Draft / Approved / Scheduled / Active / Expired / Deactivated |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | No | UTC end; required for scheduled promotion |
| CombinationMode | tinyint | No | ExclusiveBest / ExplicitStack |
| CanCombineWithProductPromotions | bit | No | Explicit Central setting |
| CanCombineWithBasketPromotions | bit | No | Explicit Central setting |
| CanCombineWithCoupons | bit | No | Explicit Central setting |
| MaxBenefitQuantityPerOrder | decimal(18,3) | Yes | Optional benefit cap |
| MaxBenefitQuantityPerCustomer | decimal(18,3) | Yes | Optional benefit cap |
| MaxUsesPerCustomer | int | Yes | Optional use limit |
| Priority | int | No | Deterministic tie-breaker after customer-benefit comparison |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central creator |
| ApprovedAtUtc | datetime2(3) | Yes | UTC |
| ApprovedByUserId | uniqueidentifier | Yes | Central approver |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- FK PromotionVersionId → PromotionVersions; restrict delete.
- Unique PromotionCode + Version.
- Check Version > 0; EffectiveToUtc > EffectiveFromUtc; maximum limits are null or non-negative.
- Published/active versions are immutable; modifications create a new version.
- Product-level and basket-level scope is explicit.
- Combination flags are set by Central; absence of an explicit allow means no stacking.
- The engine first evaluates eligible product-level promotions, chooses the best customer result unless explicit stacking is enabled, then evaluates basket-level promotions/coupons according to the configured combination rules.
- The same promotion/discount identity cannot be applied twice to the same transaction line.

### 4.3 `pricing.PromotionConditions` — Central; approved Branch projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PromotionConditionId | uniqueidentifier | No | PK; sequential Guid |
| PromotionId | uniqueidentifier | No | FK → Promotions |
| ConditionGroup | int | No | Groups conditions for AND/OR evaluation |
| LogicalOperator | tinyint | No | AND / OR within approved condition group |
| ConditionType | tinyint | No | Stable condition type code |
| Operator | tinyint | No | Equals / In / GreaterThan / GreaterOrEqual / LessThan / Between etc. as supported |
| ProductId | uniqueidentifier | Yes | Optional Product scope |
| ProductCategoryId | uniqueidentifier | Yes | Optional Category scope |
| MinimumQuantity | decimal(18,3) | Yes | Optional quantity threshold |
| MinimumOrderAmount | decimal(18,3) | Yes | Optional basket amount threshold |
| ValueText | nvarchar(500) | Yes | Optional typed comparison value |
| ValueNumber | decimal(18,3) | Yes | Optional typed comparison value |
| IsActive | bit | No | Part of the promotion version |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- FK Promotion; restrict delete.
- Check ConditionGroup >= 0 and numeric thresholds are non-negative where used.
- A condition row populates only the value fields required by its ConditionType; validate in application/domain logic.
- Product/category references are local FKs in Central; Branch projection uses local catalog IDs only where present.
- The exact allowlisted ConditionType/Operator catalogue is a code-level contract versioned with promotion schema.

### 4.4 `pricing.PromotionRewards` — Central; approved Branch projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PromotionRewardId | uniqueidentifier | No | PK; sequential Guid |
| PromotionId | uniqueidentifier | No | FK → Promotions |
| RewardType | tinyint | No | Stable reward type code |
| TargetProductId | uniqueidentifier | Yes | Optional target Product |
| TargetProductUnitId | uniqueidentifier | Yes | Optional target selling unit |
| DiscountPercent | decimal(9,4) | Yes | Percentage reward where applicable |
| DiscountAmount | decimal(18,3) | Yes | Fixed monetary reward where applicable |
| FixedUnitPrice | decimal(18,3) | Yes | Fixed-price reward where applicable |
| RewardQuantity | decimal(18,3) | Yes | Free/bonus quantity where applicable |
| MaximumDiscountAmount | decimal(18,3) | Yes | Optional cap |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- FK Promotion; restrict delete.
- Numeric reward values are non-negative and validated against RewardType; exactly the required value fields are populated.
- Target product/unit must be consistent where both are supplied.
- Reward types and rounding behavior are stable application contracts; no arbitrary code execution or expression text is stored in the database.

## 5. Coupons and redemption records

### 5.1 `pricing.Coupons` — Central; approved Branch projection where permitted

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CouponId | uniqueidentifier | No | PK; sequential Guid |
| PromotionId | uniqueidentifier | No | FK → Promotions |
| CouponCode | nvarchar(80) | No | Unique normalized code |
| CouponStatus | tinyint | No | Draft / Active / Suspended / Expired |
| ValidFromUtc | datetime2(3) | No | UTC |
| ValidToUtc | datetime2(3) | No | UTC end |
| MinimumOrderAmount | decimal(18,3) | Yes | Optional minimum basket amount |
| MaximumTotalUses | int | Yes | Optional global use limit |
| MaximumUsesPerCustomer | int | Yes | Optional per-customer limit |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor |
| ApprovedByUserId | uniqueidentifier | Yes | Central approver |
| ApprovedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique normalized CouponCode.
- Coupon must reference a valid Promotion version and cannot extend beyond its promotion validity unless the approved rules explicitly allow it.
- Check ValidToUtc > ValidFromUtc; usage limits are null or positive.
- Central alone issues, edits, suspends and publishes coupons.
- Coupon validation checks validity, eligibility, minimum order and usage limits before application.

### 5.2 `pricing.PromotionRedemptions` — Branch durable event; Central authority/consolidated history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PromotionRedemptionId | uniqueidentifier | No | PK; generated at transaction origin and preserved centrally |
| PromotionId | uniqueidentifier | No | Promotion version GUID |
| CustomerId | uniqueidentifier | Yes | Customer GUID where known |
| TransactionType | tinyint | No | POSInvoice / OnlineOrder |
| TransactionId | uniqueidentifier | No | Invoice or Order GUID; logical reference |
| TransactionLineId | uniqueidentifier | Yes | Line GUID when promotion is line-specific |
| BranchId | uniqueidentifier | Yes | Branch origin for POS redemption |
| AppliedQuantity | decimal(18,3) | Yes | Quantity counted against promotion cap |
| DiscountAmount | decimal(18,3) | No | Actual discount applied |
| AppliedAtUtc | datetime2(3) | No | UTC |
| OperationId | uniqueidentifier | No | Idempotency key |
| RowVersion | rowversion | No | Concurrency token |

### 5.3 `pricing.CouponRedemptions` — Branch durable event; Central authority/consolidated history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CouponRedemptionId | uniqueidentifier | No | PK; generated at origin and preserved centrally |
| CouponId | uniqueidentifier | No | Coupon GUID |
| PromotionRedemptionId | uniqueidentifier | No | FK to local PromotionRedemptions copy where colocated; logical GUID otherwise |
| CustomerId | uniqueidentifier | Yes | Customer GUID |
| TransactionType | tinyint | No | POSInvoice / OnlineOrder |
| TransactionId | uniqueidentifier | No | Invoice or Order GUID |
| BranchId | uniqueidentifier | Yes | Branch origin for POS |
| RedeemedAtUtc | datetime2(3) | No | UTC |
| RedemptionStatus | tinyint | No | Applied / Reversed / Rejected |
| OperationId | uniqueidentifier | No | Idempotency key |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Promotion → PromotionConditions and PromotionRewards is 1:N.
- Coupon → CouponRedemptions is 1:N.
- Promotion → PromotionRedemptions is 1:N.
- Unique OperationId per processing scope and unique promotion/transaction/line combination prevent duplicate use.
- Coupon code + transaction is unique; repeated checkout/POS retries return the original redemption result.
- CouponRedemptions is the coupon-specific usage record; PromotionRedemptions is the general promotion usage record. Do not count a coupon twice when enforcing a shared promotion limit.
- Online redemption is centrally validated. Branch Offline redemption is durably recorded locally and synchronized idempotently.
- Usage limits are checked transactionally against the authoritative local/central scope available to the operation. Cross-branch global limits during disconnected Offline operation are a technical risk to resolve without changing approved business policy.

## 6. Calculation and snapshot boundaries

- Product price lookup resolves the active Product + ProductUnit price, applying a branch-specific exception only if Central has explicitly published one; otherwise use the uniform company-wide price.
- The calculation engine evaluates only the latest valid local publication in Branch Offline mode.
- Product-level promotions are evaluated first. Eligible competing promotions produce the best customer outcome unless explicit stacking is enabled.
- Basket-level promotions and coupons do not combine automatically; explicit Central combination settings are required.
- Loyalty points are applied after discounts under the approved loyalty rules.
- Promotion and coupon usage is recorded once per transaction/line and is idempotent.
- POS invoice lines and online order lines retain snapshots of base unit price, applied promotion/coupon identifiers and versions, discount amounts, final unit/line values and calculation context. A later price/promotion change never recalculates a completed historical transaction.
- If an open POS cart's price/promotion expires or changes, the cart is recalculated and the cashier must confirm the displayed change before payment.
- JOD final invoice amounts are rounded to two decimal places. Internal values retain the approved precision; the exact half-rounding tie-breaker remains a pre-implementation technical decision.

## 7. Requirement traceability

| Approved requirement/decision | Schema support |
|---|---|
| PRC-01–PRC-04 | Central PriceVersions/ProductPrices; immutable version history and actor/effective period |
| PRC-05–PRC-07 | Promotions, conditions, rewards, Coupons and redemption records |
| PRC-08–PRC-10 / GAP-14 | BranchPricingSnapshots and versioned local projections; expiry/base-price fallback and historical snapshots |
| PRC-11 | Decimal precision and JOD final rounding policy |
| PRC-12 | Effective dates, cart reprice and confirmation; application workflow over price versions |
| PRM-01–PRM-03 / GAP-13 | Combination flags, best-customer outcome, benefit caps, product-first and basket/coupon ordering |
| CUP-01–CUP-03 | Centrally issued Coupons with validity, minimum order and usage limits |
| UNT-02 | ProductPrices keyed by ProductUnitId |

## 8. Technical risks for review

1. Define the exact PromotionCondition types/operators and supported RewardTypes; keep them allowlisted and versioned.
2. Define how “best customer outcome” is calculated when two eligible promotions have equal monetary value; Priority is a deterministic tie-breaker only.
3. Define exact precision and half-rounding tie-breaker before implementation; do not round internal quantities or unit prices prematurely.
4. Define how promotion usage caps are enforced across branches during Offline operation. A disconnected branch cannot observe redemptions that occurred at other branches after disconnection; do not silently claim a globally exact cap unless a technical reservation/allocation mechanism is approved.
5. Define whether coupons may be used in Offline POS under the centrally published policy and what local quota/usage behavior applies; the redemption event is designed for durable synchronization.
6. Define publication atomicity and recovery if price data applies but promotion data fails (or vice versa); BranchPricingSnapshots must advance only when the combined approved publication is fully applied.
7. Define how branch-specific price exceptions are approved, versioned and expired without violating the uniform default.
8. Define promotion/coupon usage retention and the authoritative source for customer identity when POS is offline.

## 9. Approval boundary and next step

TD-19 specifies Pricing & Promotions only. It does not create SQL tables, EF Core entities, DDL or migrations. The next detailed group is POS, Sales, Invoices, Returns and Exchanges, followed by Payments, Online/CRM/Fulfillment, Operations, Alerts, Audit and Reporting.

**No SQL table, EF Core entity, DDL or migration is created by this document.**
