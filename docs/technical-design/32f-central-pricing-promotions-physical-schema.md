# TD-32F — Central Pricing, Promotions and Offline Usage Physical Schema Decisions

**Version:** 0.1  
**Status:** Delegated design approval — approved for physical mapping.  
**Date:** 2026-11-28  
**Depends on:** TD-19, TD-26, TD-30, TD-31 and TD-32.

## 1. Authority and publication model

- Central is the sole authority for base prices, branch-specific exceptions, promotions, coupons, approval, publication and global usage allocation.
- Branches consume read-only approved versioned snapshots. Branch users cannot create, edit, approve, extend or deactivate Central pricing/promotion/coupon definitions.
- Price and promotion/coupon data are one atomic publication unit. A Branch stages and validates the complete catalog/price/promotion/coupon payload and hash, then applies all changes in one Branch transaction and activates the new `BranchPricingSnapshots` row last. Any failure rolls back the whole apply; the active version does not advance partially.
- A Branch maintains exactly one active `BranchPricingSnapshots` row. It records both PriceVersionNumber and PromotionVersionNumber plus the combined canonical payload hash.
- On reconnect, Central publication and Branch apply are idempotent by publication/version and payload hash.

## 2. Base pricing

1. Price is defined per Product + ProductUnit. Every sellable unit has its own active price.
2. Company-wide price is the default. A branch-specific exception exists only when explicitly approved by Central and published in the same versioned snapshot.
3. Price rows are immutable once published. Corrections create a new price version and new effective-dated rows.
4. Branch Offline uses the latest successfully applied approved base-price snapshot as its fallback; Branch never invents or locally edits a price.
5. A price change/expiry affecting an open POS cart forces recalculation and cashier confirmation before payment.
6. JOD is the only enabled selling currency in the first release; prices remain decimal(18,3), and the final invoice rounding boundary follows TD-31 AwayFromZero to two decimals.

## 3. Promotion condition catalogue

Only the following versioned application condition types are supported initially:

| ConditionType | Meaning | Required data |
|---|---|---|
| 1 | Product match | ProductId |
| 2 | Category match | ProductCategoryId |
| 3 | Minimum quantity | MinimumQuantity |
| 4 | Minimum basket amount | MinimumOrderAmount |

Allowed operators are Equals=1, In=2, GreaterThan=3, GreaterOrEqual=4, LessThan=5, LessOrEqual=6 and Between=7, but each ConditionType has a narrower valid operator set enforced by the application contract. Numeric conditions use decimal(18,3). ValueText/ValueNumber are populated only where required by the type.

ConditionGroup semantics:
- Rows in one ConditionGroup use one consistent LogicalOperator (AND=1 or OR=2).
- Conditions within the group are evaluated using that operator.
- Different groups are combined using AND.
- A mixed LogicalOperator within one group is invalid.
- Product and category references must belong to the same published product catalogue version as the promotion.

No arbitrary SQL, C#, script, expression tree, or executable JSON expression is stored or evaluated.

## 4. Promotion reward catalogue

Only the following versioned reward types are supported initially:

| RewardType | Meaning | Required fields |
|---|---|---|
| 1 | Percentage discount | DiscountPercent |
| 2 | Fixed monetary discount | DiscountAmount |
| 3 | Fixed unit price | FixedUnitPrice |
| 4 | Free/bonus quantity | RewardQuantity and target Product/Unit where the bonus is a separate product |

- Exactly the fields required by RewardType are populated; irrelevant reward columns remain null.
- Numeric reward values and maximum discount are non-negative.
- Product/unit target consistency is validated.
- Product-level and basket-level scopes remain explicit. No reward can be applied twice to the same eligible quantity/value unless the approved stacking configuration explicitly permits it.
- Adding a new condition/operator/reward type requires a versioned code contract, tests and a reviewed schema compatibility change.

## 5. Deterministic promotion precedence and stacking

- Promotions do not stack by default.
- An explicitly stackable promotion must have Central-approved compatibility flags for product, basket and coupon combination.
- Eligible promotions are evaluated in deterministic order: configured Priority descending, then PromotionId ascending.
- For non-stackable conflicts, the first eligible promotion in that order is selected. For stackable promotions, the engine applies eligible promotions in that order only while compatibility and non-double-consumption rules remain satisfied.
- Product-level promotions are evaluated before basket-level promotions. Coupons combine only where explicit Central configuration permits. Loyalty redemption is applied after discounts and remains Online-only.
- The same promotion/coupon identity cannot be applied twice to the same transaction line.
- Completed invoice/order snapshots retain the promotion IDs/versions, base price, reward details, calculation context and discount amounts. Later edits never rewrite history.

## 6. Offline promotion and coupon usage

1. Branch may apply only a complete Central-approved snapshot that is within its configured validity period. An expired promotion is not applied; the approved base price remains the fallback.
2. Branch never creates or extends a promotion/coupon snapshot locally.
3. A promotion/coupon with a cross-Branch global or per-customer usage limit cannot be redeemed Offline unless Central has issued a finite Branch-specific quota that guarantees the cap.
4. A promotion with `MaxUsesPerCustomer` or `MaxBenefitQuantityPerCustomer` requires a customer-specific quota for Offline use. If the customer is unknown or no valid quota exists, reject that capped Offline promotion.
5. A coupon with `MaximumTotalUses` or `MaximumUsesPerCustomer` requires a Branch quota; a per-customer limit requires a customer-specific quota. Without it, reject Offline redemption.
6. Uncapped promotions/coupons may be used Offline against the approved snapshot, subject to ordinary validity and local transaction constraints.
7. Quota consumption and the local redemption event are committed atomically in the Branch database. Duplicate OperationId/redemption retries return the original outcome.
8. Central allocates quota transactionally so the sum of issued Branch quotas never exceeds the global promotion/coupon cap. Reconciliation does not retroactively invalidate a valid quota-backed redemption.
9. Branches cannot transfer, increase or extend quota locally. Unused quota can be reclaimed/reallocated only by a Central command after the Branch has synchronized and acknowledged the quota state.

### 6.1 New Central table `pricing.PromotionUsageQuotas`

Stores Central-issued finite usage/quantity quota per Branch, optionally scoped to a customer.

Required fields:
- PromotionUsageQuotaId (Guid PK), PromotionId, BranchId, nullable CustomerId, AllocatedUses, ConsumedUses, AllocatedQuantity, ConsumedQuantity, ValidFromUtc, ValidToUtc, QuotaStatus, IssuedAtUtc, IssuedByUserId, RowVersion.
- Central FK to Promotion and Branch; optional Customer FK.
- A Branch-local projection uses the same quota ID and Central source identifiers but no cross-database FK.
- Allocated/consumed counters are non-negative; consumed cannot exceed allocated.
- Unique active quota scope per Promotion + Branch + nullable Customer using filtered indexes for customer-scoped and branch-wide rows.
- Central aggregate issuance is transactionally capped by the promotion's configured global/per-customer limits.

### 6.2 New Central table `pricing.CouponUsageQuotas`

Stores Central-issued finite coupon redemption quota per Branch, optionally scoped to a customer.

Required fields:
- CouponUsageQuotaId (Guid PK), CouponId, BranchId, nullable CustomerId, AllocatedUses, ConsumedUses, ValidFromUtc, ValidToUtc, QuotaStatus, IssuedAtUtc, IssuedByUserId, RowVersion.
- Central FK to Coupon and Branch; optional Customer FK.
- Branch-local projection uses the same quota ID and no cross-database FK.
- Allocated/consumed counters are non-negative; consumed cannot exceed allocated.
- Unique active quota scope per Coupon + Branch + nullable Customer using filtered indexes.
- Central ensures total quota allocation never exceeds MaximumTotalUses and customer-scoped allocation never exceeds MaximumUsesPerCustomer.

## 7. Redemption history and idempotency

- `PromotionRedemptions` is the general promotion-use event; `CouponRedemptions` is the coupon-specific event. A coupon redemption is not counted twice against a shared promotion limit.
- Branch creates durable local redemption evidence; Central retains the authoritative consolidated history after synchronization.
- Unique operation/transaction scope prevents duplicate redemptions. Same OperationId and same payload returns the original outcome; same key with a different payload is an integrity conflict.
- Redemption reversal is a compensating event/status transition; do not delete the original redemption.
- Offline quota counter decrement and redemption insertion are one Branch transaction.

## 8. Physical index and consistency requirements

- Unique PriceVersionNumber and PromotionVersionNumber.
- Unique ProductPrice per version/Product/ProductUnit/company-or-Branch scope, with separate filtered indexes for BranchId IS NULL and BranchId IS NOT NULL.
- Unique PromotionCode + Version; unique normalized CouponCode.
- Published versions and child price/promotion/coupon rows are immutable.
- Filtered unique active BranchPricingSnapshots row.
- Filtered unique active quota scope for each promotion/coupon and Branch/customer scope.
- Check positive version numbers, valid effective date ranges, non-negative monetary/quantity/usage values and allowed status/type values.
- All local projection relationships use local FKs only; Central-to-Branch references are GUID correlations.

## 9. Required tests

- Atomic combined price/promotion publication with failure at each apply stage.
- Stale publication replay, same version/same hash, and same version/different hash.
- Branch-specific price exception and uniform fallback.
- Expired promotion with still-valid base price.
- Promotion ordering and deterministic tie-breaker.
- Explicit stacking compatibility and prevention of double-consumed quantity.
- Each ConditionType/Operator and RewardType schema validation.
- Global/per-customer coupon and promotion quota allocation cap under concurrent issuance.
- Offline quota consumption racing two POS transactions; duplicate redemption replay; reconnect reconciliation; quota revocation/reallocation.
- No Offline redemption of capped offers without a valid quota.

## 10. Approval record

The owner delegated detailed design decisions on 2026-11-28. The pricing/promotion physical decisions and initial allowlisted operator/reward catalogue are approved for mapping. The quota tables must be reconciled into TD-13/TD-14 and branch projection inventory before migrations.
