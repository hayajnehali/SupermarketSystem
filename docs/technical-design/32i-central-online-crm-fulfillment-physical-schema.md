# TD-32I — Central Online Commerce, Customer, Loyalty and Fulfillment Physical Mapping

**Version:** 0.1  
**Status:** Implementation baseline — aligned to approved TD-22 and TD-32 register  
**Date:** 2026-11-28  
**Depends on:** TD-12, TD-19, TD-20, TD-21, TD-22, TD-26, TD-29, TD-30, TD-31 and TD-32.

> This document translates the already-approved TD-22 rules into the Central/Branch physical implementation boundary. It does not authorize migrations until the full register and cross-module review are complete.

## 1. Authority and database placement

| Area | Central authority | Branch responsibility |
|---|---|---|
| Customer identity/profile | `crm.Customers`, phone verification and duplicate resolution | No customer master maintenance |
| Cart and online order | `online.*` authoritative | Assigned-order projection only |
| Checkout reservation | Central lifecycle and 15-minute deadline | Branch accepts/rejects physical stock reservation atomically |
| Delivery policy | Central cities, areas, fee/free-delivery rules and store policy | Consumes approved assignment details |
| Loyalty | Central rules, accounts, lots, ledger and redemption authority | May submit permitted earning facts; no redemption |
| Complaints/disputes | Central workflow and decision | Supplies execution facts when requested |
| Fulfillment | Central task creation, assignment, exception decisions and dispute authority | Physical picking, packing, handover, pickup and delivery execution |

All cross-database identifiers are correlation GUIDs only. Do not create cross-database foreign keys or attempt distributed SQL transactions.

## 2. Central table groups

The following TD-22 tables are Central-owned. The existing TD-22 column definitions remain authoritative unless this physical mapping explicitly clarifies an implementation constraint.

### 2.1 Customer and verification

- `crm.Customers`
- `crm.CustomerPhoneVerifications`
- `crm.CustomerDuplicateResolutions`

Physical requirements:
- `Customers.UserId` is a unique FK to `auth.Users.UserId`; `AccountType=Customer` is validated in the application layer per TD-29.
- Unique indexes: `CustomerNumber`, normalized phone, and `UserId`.
- Normalize phone to one canonical international representation before uniqueness checks; do not store multiple formatting variants as distinct customers.
- Verification rows store only OTP hashes, expiry, attempt state and provider reference. OTP plaintext is never persisted.
- Concurrent verification attempts use rowversion plus a conditional status/expiry update; a code can be consumed successfully only once.
- Duplicate resolution preserves both customer identities and historical transaction references. A canonical-customer lookup resolves the duplicate; no destructive reassignment of historical order/payment records.

### 2.2 Cart and online order

- `online.Carts`, `online.CartLines`
- `online.Orders`, `online.OrderLines`, `online.OrderStatusHistory`
- `online.OrderReservations`, `online.OrderDeliveryDetails`
- `online.DeliveryCities`, `online.DeliveryAreas`, `online.DeliveryFeeRules`, `online.FreeDeliveryRules`, `online.StorePolicies`
- `online.OrderCancellationRequests`, `online.OrderCancellationDecisions`, `online.OrderCancellationDecisionLines`

Physical requirements:
- Unique indexes: `Orders.OrderNumber`; `OrderReservations.OrderId`; `OrderReservations.OperationId`; `Carts` may have at most one Active cart per customer (filtered unique index).
- `CartLines` unique by CartId + ProductId + ProductUnitId; quantity is positive and validated against the product unit precision.
- The order header and all order lines, price/promotion snapshots, delivery detail, reservation intent and Outbox message are committed in one Central transaction. The physical stock reservation itself is accepted in the Branch database and acknowledged asynchronously through TD-12.
- A checkout OperationId is stable across retries. Replaying it returns the original order/reservation result and must not create another order or reservation.
- The Central reservation expiry is exactly 15 minutes from the persisted reservation start. Expiry processing is idempotent and sends a release command through `sync.BranchCommands`; a delayed command cannot release a different/newer reservation.
- A payment callback arriving after expiry is recorded against the original order/payment attempt, cannot reopen the order or stock reservation, and follows the approved late-payment handling path in TD-21.
- Order status, payment status, refund status and fulfillment status remain separate state machines. Do not collapse them into a single status column.
- Price, product name, SKU, unit, discount, delivery fee, loyalty redemption and final amount are immutable order snapshots after confirmation.
- Final payable amount follows TD-31: retain precision in intermediate calculations and apply AwayFromZero rounding only at the final invoice/payment boundary.
- Delivery address is per order only. No reusable customer-address table, guest checkout, split order, customer-selected delivery time, or automatic substitution is introduced.
- Delivery fee rules must have non-overlapping effective periods for the same applicable area/scope. The checkout selects exactly one effective rule or a valid free-delivery rule; ambiguity blocks checkout rather than selecting arbitrarily.
- `FreeDeliveryRules.ConditionPayload` is versioned structured data validated against a closed allowlist. It is not executable code, SQL, or arbitrary expressions.
- `OrderStatusHistory` is append-only. A state transition and its history row are persisted in the same transaction.

### 2.3 Customer loyalty and complaints

Central-owned tables:
- `crm.LoyaltyProgramRules`
- `crm.LoyaltyAccounts`
- `crm.LoyaltyPointTransactions`
- `crm.LoyaltyPointLots`
- `crm.LoyaltyPointLotAllocations`
- `crm.CustomerComplaints`
- `crm.ComplaintActions`
- `crm.ComplaintAssignments`

Physical requirements:
- Loyalty balance is derived from the immutable point ledger; a cached balance may be maintained only in the same transaction as the ledger update and protected by rowversion/conditional updates.
- Earned points are represented as identifiable lots with expiry/rule snapshot as applicable. Redemption consumes eligible lots deterministically (earliest expiry first, then creation time, then LotId) and records each consumption in `LoyaltyPointLotAllocations`.
- A redemption request must atomically verify customer status, available non-expired points, applicable rule/version and order eligibility. Online redemption only; Branch POS redemption is not enabled in this release.
- Points are earned only after completed sale and confirmed payment. Returns create compensating negative ledger transactions and reverse earned points proportionally; never edit/delete the original earning transaction.
- Offline Branch earning facts, if accepted by approved policy, are pending Central posting and cannot increase redeemable balance until Central validates and posts them.
- A complaint is linked to a customer and may reference an order by same-database FK. Actions and assignments are append-only history; resolving a complaint does not rewrite the underlying order, payment, delivery or refund facts.
- Duplicate customer resolution and loyalty consolidation must be idempotent and auditable. No duplicate point value may be introduced by retries.

### 2.4 Central fulfillment authority

Central-owned tables from TD-22 and the TD-32 register:
- `fulfillment.FulfillmentTasks`
- `fulfillment.FulfillmentExceptionDecisions`
- `fulfillment.FulfillmentExceptionDecisionLines`
- `fulfillment.DeliveryShiftCoordinators`
- `fulfillment.DeliveryAssignments`
- `fulfillment.PickupRetentionPolicies`
- `fulfillment.PickupNonCollectionCases`
- `fulfillment.PickupNonCollectionDecisions`
- `fulfillment.DeliveryDisputes`
- `fulfillment.DeliveryDisputeDecisions`

Physical requirements:
- One fulfillment task belongs to one order and one Branch. Enforce unique OrderId for the task unless TD-22 explicitly defines task versioning; task retries are represented by OperationId/attempt history, not duplicate active tasks.
- Task assignment and reassignment are explicit Central commands with actor, reason, version and timestamp. A Branch cannot self-assign another Branch's order.
- Delivery coordinator and delivery assignment references are Central-local. Branch execution uses GUID correlation only.
- Pickup retention deadline is computed from the approved policy snapshot attached to the task/order. Changing the current policy does not retroactively change existing orders.
- A non-collected pickup case is opened only after its stored deadline and is decided centrally. It cannot independently issue a refund or restore stock.
- Delivery dispute submission preserves the original delivery completion evidence and does not itself refund or cancel the order. Central decision is explicit and auditable.
- No delivery proof feature or new customer signature/photo evidence requirement is added. Persist only execution facts already defined by TD-22 and approved project decisions.

## 3. Branch operational projection and execution

Branch stores only the minimum assigned-order information and local execution evidence. It does not store a customer master or permit customer search.

### 3.1 Assigned order projection

Projection content:
- Central OrderId, OrderNumber, source version and synchronization metadata.
- Order lines: ProductId/ProductUnitId, SKU, product-name snapshot, quantity and approved price/discount snapshot.
- Fulfillment type and current Central task/version.
- For delivery only: delivery address, instructions, contact name and contact phone.
- For pickup: only the customer name/phone needed to identify and hand over the order.
- No email, full customer profile, other orders, loyalty balance, saved address book or customer search index.

Projection is immutable by Branch users. Updates are accepted only as newer authenticated Central versions. A stale or out-of-order event cannot overwrite a newer version.

### 3.2 Branch execution

Branch records local facts for:
- Picking start/completion and picked quantities.
- Approved exception requests for unavailable/damaged items; no automatic substitution.
- Packing/ready-for-pickup or ready-for-delivery.
- Handover and delivery/pickup completion status already allowed by TD-22.
- Failed delivery and return-to-Branch handling.
- Pickup not collected evidence and stock return/quarantine outcome.

Use the exact Branch table names and detailed columns already defined by TD-22. Do not duplicate Central authority tables locally. Each execution command has an OperationId and is committed with the Branch Outbox event in the same local transaction.

No delivery proof capability is introduced. Do not add customer signatures, GPS trails, photographs, or proof-of-delivery document requirements.

## 4. Required indexes, constraints and concurrency

| Table/group | Required physical enforcement |
|---|---|
| Customers | Unique normalized phone, CustomerNumber and UserId |
| Active carts | Filtered unique Active cart per CustomerId |
| CartLines | Unique CartId + ProductId + ProductUnitId |
| Orders | Unique OrderNumber; index CustomerId + CreatedAtUtc; index BranchId + OrderStatus + CreatedAtUtc |
| OrderReservations | Unique OrderId and OperationId; index ReservationStatus + ExpiresAtUtc |
| OrderStatusHistory | Index OrderId + ChangedAtUtc; append-only |
| DeliveryFeeRules | Index AreaId + EffectiveFromUtc + EffectiveToUtc; application rejects overlapping active periods |
| LoyaltyPointTransactions | Unique OperationId/BusinessReference where applicable; immutable ledger |
| LoyaltyPointLots | Index LoyaltyAccountId + ExpiryAtUtc + CreatedAtUtc |
| LoyaltyPointLotAllocations | Unique source redemption transaction + lot |
| FulfillmentTasks | Unique OrderId; index BranchId + TaskStatus + CreatedAtUtc |
| DeliveryAssignments | Index CoordinatorId + AssignmentStatus; enforce one active assignment per task |
| Complaints/Disputes | Index CustomerId/OrderId + CreatedAtUtc; append-only decisions/actions |

All mutable workflow headers use rowversion. Critical balances, reservation state and assignment state use conditional updates or short transactions; do not rely on a read-then-write check without concurrency protection.

## 5. Transaction and integration boundaries

1. Central checkout transaction persists order, lines, immutable commercial snapshots, reservation intent and Outbox event.
2. Branch reservation command is idempotent and atomically reserves exact eligible batch quantities under the same stock concurrency rules as POS.
3. Central confirms the reservation only after Branch acceptance; a rejected reservation cancels checkout and releases any partial local reservation atomically.
4. Successful payment confirmation and order transition are idempotent and recorded in Central with the provider event.
5. Fulfillment task creation is triggered only after valid payment confirmation (or the explicitly permitted BranchCreatedPaidPickup source) and accepted stock reservation.
6. Branch picking/handover events are committed locally with Outbox and consumed centrally through durable Inbox/ProcessedOperations.
7. Central decisions (release reservation, approve exception, cancel, assignment) are delivered through TD-12 BranchCommands. The Branch applies each command once by OperationId.
8. No operation assumes an atomic transaction spanning Central and Branch databases.

## 6. Migration gate

Before creating EF Core mappings/migrations:
- Verify every table and column against TD-22 and TD-32; do not silently omit any approved TD-22 table.
- Confirm exact Branch execution table names and their ownership.
- Reconcile all relationships with TD-14 and remove cross-database FKs.
- Add the table groups to the final Central/Branch DbContext inventory.
- Add SQL Server integration tests for checkout idempotency, reservation expiry/release races, loyalty concurrent redemption, and out-of-order fulfillment events.
