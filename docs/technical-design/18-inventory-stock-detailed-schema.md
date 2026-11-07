# TD-18 — Detailed Logical Schema: Inventory and Stock

**Version:** 0.1  
**Status:** Proposed for owner review  
**Date:** 2026-11-07  
**Scope:** Branch-owned physical inventory, warehouse locations, batches/cost, movement ledger and balances, website reservations, stocktaking, inter-branch transfers, quarantine/disposal, reorder thresholds and expiry alerts.  
**Depends on:** Approved BRS Unit 04, approved inventory/transfer/stocktake decisions INV-01–INV-22, TD-03, TD-04, TD-05, TD-09, TD-12, TD-13 and TD-14.

> This is a logical/physical table specification for review, not executable DDL, EF Core source code or a migration. It preserves Central authority for approvals and Branch authority for physical stock. It does not create cross-database FKs or distributed transactions.

## 1. Shared inventory conventions

1. Every table PK is sequential application-generated `Guid` / SQL Server `uniqueidentifier`; no integer identity PK.
2. Quantities use `decimal(18,3)`; no float/double. Unit conversions are applied using the approved product-unit conversion rules.
3. Batch cost and historical COGS use `decimal(18,3)` for JOD and are retained per batch/issue; no weighted-average costing.
4. UTC event times use `datetime2(3)`; expiry dates use `date`.
5. Branch is authoritative for its physical stock ledger, batches, balances, reservations, quarantine and actual movement execution. Central has synchronized projections and owns the approved control workflows.
6. A branch database contains only its own stock. Branch A never reads/writes Branch B inventory.
7. No stock-changing operation may bypass the Branch transaction that serializes it against reservations, stocktake locks, sellability, batch availability and other stock movements.
8. Product, ProductUnit and Branch identifiers in a Branch database refer to approved local projections/configuration. Never create a SQL FK to a Central database table.
9. Inventory ledger and audit history are append-only in ordinary workflows. Corrections use compensating movements, not destructive edits.
10. `InventoryBalances` is a controlled projection derived from posted movements/reservations/quarantine. The movement ledger remains the source of historical truth.

## 2. Database placement

| Table | Central DB | Each Branch DB |
|---|---|---|
| Warehouses, WarehouseLocations | Reference/projection | Authoritative |
| InventoryBatches, InventoryMovements, InventoryMovementLines | Immutable synchronized projection | Authoritative |
| InventoryBalances | Consolidated read projection | Authoritative operational projection |
| StockReservations, StockReservationAllocations | Coordination/projection | Authoritative reservation acceptance and stock exclusion |
| Stocktakes, scope, assignments, counts, recounts, variance decisions, session events | Authoritative | No count authority |
| StocktakeLocks | No | Authoritative local movement freeze |
| StocktakeFreezeAcknowledgements | Authoritative acknowledgement record | Sends durable acknowledgement |
| StockTransfers, lines, Central events | Authoritative workflow and physical transfer control | No authority to execute; receives commands and records local physical stock movements/acknowledgements |
| StockTransferBranchEvents | Synchronized history | Branch-originated execution facts |
| InventoryQuarantines | Synchronized projection | Authoritative local quarantine |
| InventoryDisposalRequests/Decisions | Central approval authority | Branch request origin |
| InventoryDisposals/Lines | Synchronized evidence | Authoritative physical disposal evidence |
| ReorderPolicies, ExpiryAlertPolicies | Authoritative policy | Approved local projection |
| StockIncreaseRequests | Central review copy | Branch request origin |
| StockIncreaseRequestDecisions | Authoritative | No |

## 3. Warehouses and locations

### 3.1 `inventory.Warehouses` — Branch

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| WarehouseId | uniqueidentifier | No | PK; sequential Guid |
| WarehouseCode | nvarchar(30) | No | Unique within Branch |
| Name | nvarchar(120) | No | Warehouse name |
| IsDefault | bit | No | At most one active default warehouse |
| IsActive | bit | No | Deactivation only |
| CreatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- Unique WarehouseCode.
- Filtered unique index for one active default warehouse.
- Branch database is branch-scoped; no FK to Central Branches.

### 3.2 `inventory.WarehouseLocations` — Branch

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| WarehouseLocationId | uniqueidentifier | No | PK; sequential Guid |
| WarehouseId | uniqueidentifier | No | FK → Warehouses |
| ParentLocationId | uniqueidentifier | Yes | Self-FK for optional nested locations |
| LocationCode | nvarchar(40) | No | Unique within Warehouse |
| Name | nvarchar(120) | No | Location name |
| LocationType | tinyint | No | SalesFloor / Backroom / Refrigerator / Other |
| IsSellableLocation | bit | No | Whether eligible stock may be sold from this location |
| IsActive | bit | No | Deactivation only |
| RowVersion | rowversion | No | Concurrency token |

**Constraints**
- Unique WarehouseId + LocationCode.
- FK Warehouse and optional ParentLocation; restrict delete.
- Prevent self-parenting and deeper cycles in application validation.
- Supports branch sales floor, backroom, refrigerators and other approved locations.

## 4. Inventory batches and balances

### 4.1 `inventory.InventoryBatches` — Branch authoritative

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InventoryBatchId | uniqueidentifier | No | PK; sequential Guid |
| ProductId | uniqueidentifier | No | Central product GUID; local FK only if local Catalog projection exists |
| ProductUnitId | uniqueidentifier | No | Central unit GUID; local FK only if local Catalog projection exists |
| WarehouseId | uniqueidentifier | No | FK → Warehouses |
| SourcePurchaseReceiptLineId | uniqueidentifier | Yes | Source receipt-line GUID; local FK to Procurement projection only if colocated |
| SourceStockTransferLineId | uniqueidentifier | Yes | Source transfer-line GUID; logical reference |
| BatchCode | nvarchar(80) | Yes | Supplier/manufacturer batch code when supplied |
| ReceivedAtUtc | datetime2(3) | No | UTC |
| ExpiryDate | date | Yes | Required only if Product.RequiresExpiryTracking |
| OriginalQuantity | decimal(18,3) | No | Accepted quantity initially posted |
| UnitCost | decimal(18,3) | No | Immutable cost per inventory unit |
| BatchStatus | tinyint | No | Active / Quarantined / Expired / Depleted / Closed |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Check OriginalQuantity > 0 and UnitCost >= 0.
- ExpiryDate is mandatory only for products configured for expiry tracking; non-expiry products may have null expiry.
- Prevent sale issue from expired, quarantined, depleted or otherwise ineligible batches.
- Index ProductId + ExpiryDate + ReceivedAtUtc for FEFO/FIFO selection.
- Batch cost is immutable after receipt posting; cost corrections require an audited compensating workflow, not in-place rewriting.
- Current remaining quantity is represented by InventoryBalances/ledger, not by rewriting OriginalQuantity.

### 4.2 `inventory.InventoryBalances` — Branch operational projection; Central reporting projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InventoryBalanceId | uniqueidentifier | No | PK; sequential Guid |
| ProductId | uniqueidentifier | No | Product GUID |
| ProductUnitId | uniqueidentifier | No | Unit GUID |
| WarehouseLocationId | uniqueidentifier | No | FK → local WarehouseLocations |
| InventoryBatchId | uniqueidentifier | No | FK → local InventoryBatches |
| OnHandQuantity | decimal(18,3) | No | Physically held quantity |
| ReservedQuantity | decimal(18,3) | No | Quantity reserved for approved orders |
| QuarantinedQuantity | decimal(18,3) | No | Quantity excluded due to quarantine |
| OtherUnavailableQuantity | decimal(18,3) | No | Other approved non-sellable quantity |
| UpdatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique ProductId + ProductUnitId + WarehouseLocationId + InventoryBatchId.
- FKs to local WarehouseLocations and InventoryBatches.
- Check all quantities are non-negative; Reserved + Quarantined + OtherUnavailable cannot exceed OnHand.
- Available quantity is derived as OnHand − Reserved − Quarantined − OtherUnavailable and is additionally constrained by expiry, product status, location sellability and active stocktake locks.
- Balance updates occur in the same Branch transaction as the corresponding movement/reservation/quarantine effect.
- Reconciliation can rebuild this projection from the ledger; it cannot silently change the immutable movement history.

## 5. Stock movement ledger

### 5.1 `inventory.InventoryMovements` — Branch authoritative; Central synchronized history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InventoryMovementId | uniqueidentifier | No | PK; sequential Guid |
| MovementNumber | nvarchar(50) | No | Unique Branch-readable reference if required |
| MovementType | tinyint | No | Receipt / SaleIssue / SaleReturn / LocationTransfer / BranchTransferOut / BranchTransferIn / Quarantine / Disposal / StocktakeAdjustment / ManualAdjustment / Reversal |
| SourceTransactionType | nvarchar(80) | No | Stable source aggregate type |
| SourceTransactionId | uniqueidentifier | No | Source transaction GUID |
| OperationId | uniqueidentifier | No | Stable idempotent operation identity |
| OccurredAtUtc | datetime2(3) | No | Business event time |
| RecordedAtUtc | datetime2(3) | No | Persistence time |
| ActorEmployeeId | uniqueidentifier | No | Employee GUID |
| DeviceId | uniqueidentifier | Yes | POS/device GUID where applicable |
| ReasonCode | nvarchar(50) | Yes | Stable reason code |
| ReasonDescription | nvarchar(500) | Yes | Required for Other/manual exceptions |
| CorrelationId | uniqueidentifier | No | Workflow trace |
| RowVersion | rowversion | No | Concurrency token |

### 5.2 `inventory.InventoryMovementLines` — Branch authoritative; Central synchronized history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InventoryMovementLineId | uniqueidentifier | No | PK; sequential Guid |
| InventoryMovementId | uniqueidentifier | No | FK → InventoryMovements |
| ProductId | uniqueidentifier | No | Product GUID |
| ProductUnitId | uniqueidentifier | No | Unit GUID |
| InventoryBatchId | uniqueidentifier | No | FK → local InventoryBatches |
| SourceLocationId | uniqueidentifier | Yes | FK → local WarehouseLocations |
| DestinationLocationId | uniqueidentifier | Yes | FK → local WarehouseLocations |
| Quantity | decimal(18,3) | No | Positive movement quantity |
| UnitCostSnapshot | decimal(18,3) | No | Batch cost snapshot |
| LineValue | decimal(18,3) | No | Quantity × cost under approved precision rules |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Movement → MovementLines is 1:N.
- Every line has a positive quantity and valid batch/unit/product relationship.
- Receipt/transfer-in requires a destination; issue/transfer-out requires a source; location transfer requires both.
- Sale issue lines retain exact batch and cost references for COGS.
- Composite product consumption is recorded by the sales module and posts corresponding component issue movements.
- Returns, disposal and stocktake/manual adjustments post new movements; posted movement rows are not edited or deleted.
- A unique operation/source-purpose key prevents duplicate posting for the same business effect; OperationId remains stable across retries.

## 6. Website stock reservations

### 6.1 `inventory.StockReservations` — Branch authoritative; Central coordination/projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StockReservationId | uniqueidentifier | No | PK; sequential Guid |
| OnlineOrderReservationId | uniqueidentifier | No | Central reservation GUID; no cross-db FK |
| OnlineOrderId | uniqueidentifier | No | Central order GUID; no cross-db FK |
| OnlineOrderLineId | uniqueidentifier | No | Central order line GUID; no cross-db FK |
| ProductId | uniqueidentifier | No | Product GUID |
| ProductUnitId | uniqueidentifier | No | Product unit GUID |
| Quantity | decimal(18,3) | No | Positive reserved quantity |
| ReservationStatus | tinyint | No | Pending / Accepted / Released / Consumed / Rejected / Expired |
| RequestedAtUtc | datetime2(3) | No | UTC |
| AcceptedAtUtc | datetime2(3) | Yes | UTC |
| ReleasedAtUtc | datetime2(3) | Yes | UTC |
| ReleaseReason | tinyint | Yes | OrderCancelled / PaymentExpired / FulfillmentComplete / CentralCommand |
| OperationId | uniqueidentifier | No | Idempotency identity |
| RowVersion | rowversion | No | Concurrency token |

### 6.2 `inventory.StockReservationAllocations` — Branch

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StockReservationAllocationId | uniqueidentifier | No | PK; sequential Guid |
| StockReservationId | uniqueidentifier | No | FK → StockReservations |
| InventoryBatchId | uniqueidentifier | No | FK → InventoryBatches |
| WarehouseLocationId | uniqueidentifier | No | FK → WarehouseLocations |
| AllocatedQuantity | decimal(18,3) | No | Positive batch/location allocation |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique OnlineOrderReservationId within the Branch DB; retries cannot create a second accepted reservation.
- Reservation → allocations is 1:N when exact batch allocation is used. One reservation may span multiple batches/locations.
- Sum of active allocations cannot exceed reservation quantity; reservation quantity cannot exceed currently available stock.
- Acceptance, reserved-balance update and durable reservation record commit in one Branch DB transaction before Central acknowledgement.
- POS sale validation and website reservation acceptance serialize against the same Branch stock/reservation state.
- Reservation release/consumption is idempotent; a duplicate central command cannot release or consume twice.
- Central 15-minute reservation/payment lifecycle remains authoritative for website checkout; Branch acceptance is authoritative for physical stock exclusion.

## 7. Central stocktaking and Branch freeze

### 7.1 `inventory.Stocktakes` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StocktakeId | uniqueidentifier | No | PK; sequential Guid |
| BranchId | uniqueidentifier | No | FK → Central org.Branches |
| StocktakeCode | nvarchar(50) | No | Unique Central reference |
| StocktakeType | tinyint | No | Full / Partial / Surprise |
| StocktakeStatus | tinyint | No | Draft / Freezing / Counting / Recount / Investigation / AwaitingClosure / Closed / Cancelled |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor |
| StartedAtUtc | datetime2(3) | Yes | UTC |
| ClosedAtUtc | datetime2(3) | Yes | UTC |
| CancelledAtUtc | datetime2(3) | Yes | UTC |
| CancellationReason | nvarchar(1000) | Yes | Required if cancelled |
| RowVersion | rowversion | No | Concurrency token |

### 7.2 `inventory.StocktakeScopeLines` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StocktakeScopeLineId | uniqueidentifier | No | PK; sequential Guid |
| StocktakeId | uniqueidentifier | No | FK → Stocktakes |
| ProductId | uniqueidentifier | No | Product GUID |
| WarehouseLocationId | uniqueidentifier | Yes | Location GUID; null only when the approved scope means all locations for the product |
| ProductUnitId | uniqueidentifier | No | Counting unit |
| ExpectedQuantitySnapshot | decimal(18,3) | No | Frozen system quantity; never shown to the counter |
| RowVersion | rowversion | No | Concurrency token |

### 7.3 `inventory.StocktakeAssignments` and `StocktakeLines` — Central

**`StocktakeAssignments`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StocktakeAssignmentId | uniqueidentifier | No | PK; sequential Guid |
| StocktakeId | uniqueidentifier | No | FK → Stocktakes |
| AssignedEmployeeId | uniqueidentifier | No | Central employee GUID |
| AssignedAtUtc | datetime2(3) | No | UTC |
| AssignedByUserId | uniqueidentifier | No | Central assigner |
| AssignmentStatus | tinyint | No | Active / Completed / Revoked |
| RowVersion | rowversion | No | Concurrency token |

**`StocktakeLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StocktakeLineId | uniqueidentifier | No | PK; sequential Guid |
| StocktakeScopeLineId | uniqueidentifier | No | FK → StocktakeScopeLines |
| StocktakeAssignmentId | uniqueidentifier | No | FK → StocktakeAssignments |
| CountedQuantity | decimal(18,3) | No | Actual blind count |
| CountedByEmployeeId | uniqueidentifier | No | Assigned Central employee |
| CountedAtUtc | datetime2(3) | No | UTC |
| CountSequence | tinyint | No | First count sequence |
| RowVersion | rowversion | No | Concurrency token |

### 7.4 `inventory.StocktakeRecounts`, `StocktakeVarianceCases` and `StocktakeVarianceDecisions` — Central

**`StocktakeRecounts`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StocktakeRecountId | uniqueidentifier | No | PK; sequential Guid |
| StocktakeLineId | uniqueidentifier | No | FK → StocktakeLines |
| RecountedQuantity | decimal(18,3) | No | Second/subsequent count |
| RecountedByEmployeeId | uniqueidentifier | No | Assigned Central employee |
| RecountedAtUtc | datetime2(3) | No | UTC |
| RecountSequence | tinyint | No | Starts at 2 |
| Reason | nvarchar(500) | Yes | Required when further recount is authorized |

**`StocktakeVarianceCases`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StocktakeVarianceCaseId | uniqueidentifier | No | PK; sequential Guid |
| StocktakeScopeLineId | uniqueidentifier | No | FK → StocktakeScopeLines |
| FinalCountQuantity | decimal(18,3) | No | Count result submitted for investigation |
| SystemQuantitySnapshot | decimal(18,3) | No | Expected quantity snapshot |
| VarianceQuantity | decimal(18,3) | No | Final count minus system quantity |
| VarianceReasonCode | tinyint | Yes | Damage / Expiry / TheftOrLoss / ReceivingError / SaleError / TransferError / Other |
| OtherReasonDescription | nvarchar(1000) | Yes | Required for Other |
| InvestigationNotes | nvarchar(2000) | Yes | Central investigation |
| CaseStatus | tinyint | No | Open / UnderInvestigation / AwaitingDecision / Resolved |

**`StocktakeVarianceDecisions`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StocktakeVarianceDecisionId | uniqueidentifier | No | PK; sequential Guid |
| StocktakeVarianceCaseId | uniqueidentifier | No | FK → StocktakeVarianceCases |
| Decision | tinyint | No | ApproveAdjustment / RejectAdjustment |
| ApprovedAdjustmentQuantity | decimal(18,3) | Yes | Required only when approved |
| Reason | nvarchar(1000) | No | Central decision reason |
| DecidedByUserId | uniqueidentifier | No | Central actor |
| DecidedAtUtc | datetime2(3) | No | UTC |

### 7.5 `inventory.StocktakeSessionEvents`, `StocktakeFreezeAcknowledgements` — Central; `StocktakeLocks` — Branch

**`StocktakeSessionEvents`** records Started, Cancelled, Closed, FreezeRequested and FreezeReleased events with actor/time/reason.

**`StocktakeFreezeAcknowledgements`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StocktakeFreezeAcknowledgementId | uniqueidentifier | No | PK; sequential Guid |
| StocktakeId | uniqueidentifier | No | Central session GUID |
| BranchId | uniqueidentifier | No | Central Branch FK |
| AcknowledgedAtUtc | datetime2(3) | No | UTC |
| AcknowledgementStatus | tinyint | No | Frozen / Failed / Released |
| BranchOperationId | uniqueidentifier | No | Branch operation correlation |

**`StocktakeLocks`** — Branch local enforcement

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StocktakeLockId | uniqueidentifier | No | PK; sequential Guid |
| StocktakeId | uniqueidentifier | No | Central session GUID; no cross-db FK |
| StocktakeScopeLineId | uniqueidentifier | No | Central scope GUID; no cross-db FK |
| ProductId | uniqueidentifier | No | Product GUID |
| WarehouseLocationId | uniqueidentifier | Yes | Location scope |
| LockedAtUtc | datetime2(3) | No | UTC |
| ReleasedAtUtc | datetime2(3) | Yes | UTC; set only after authorized central release command |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Stocktake → ScopeLines, Assignments, Lines, Events and FreezeAcknowledgements are 1:N.
- Each scope line receives one first count; recount attempts are separate rows.
- Expected quantity is captured before counting and hidden from assigned counters.
- When first count differs from expected, second count is required. Matching first/second results proceed to Central review; differing counts require investigation and are not automatically resolved.
- Each variance is a separate case. The reason list includes damage, expiry, theft/loss, receiving error, sale error, transfer error and Other with description.
- Branch applies a durable scoped lock before acknowledging freeze. All stock-changing operations for locked product/location scope are rejected until a valid Central release command is applied.
- Partial stocktake locks only the defined product/location scope; products outside scope remain operational.
- Only Central may cancel or close a session. Freeze release occurs only after all counts/recounts, variance investigations and approve/reject decisions are complete; even a no-variance session requires Central closure.
- An approved adjustment is sent to Branch as a command; Branch posts the actual compensating InventoryMovement and returns its durable acknowledgement. Central never updates Branch stock directly.

## 8. Inter-branch stock transfers

### 8.1 `inventory.StockTransfers`, `StockTransferLines` and `StockTransferEvents` — Central

**`StockTransfers`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StockTransferId | uniqueidentifier | No | PK; sequential Guid |
| TransferNumber | nvarchar(50) | No | Unique Central reference |
| SourceBranchId | uniqueidentifier | No | FK → Central org.Branches |
| DestinationBranchId | uniqueidentifier | No | FK → Central org.Branches |
| RequestedByEmployeeId | uniqueidentifier | No | Branch employee GUID |
| RequestedAtUtc | datetime2(3) | No | UTC |
| TransferStatus | tinyint | No | Requested / Approved / Rejected / Dispatched / PartiallyReceived / Received / DifferenceReview / Closed |
| ApprovedByUserId | uniqueidentifier | Yes | Central approver |
| ApprovedAtUtc | datetime2(3) | Yes | UTC |
| DispatchedByUserId | uniqueidentifier | Yes | Central employee executing dispatch |
| DispatchedAtUtc | datetime2(3) | Yes | UTC |
| ReceivedByUserId | uniqueidentifier | Yes | Central employee executing physical receipt |
| ReceivedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

**`StockTransferLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StockTransferLineId | uniqueidentifier | No | PK; sequential Guid |
| StockTransferId | uniqueidentifier | No | FK → StockTransfers |
| ProductId | uniqueidentifier | No | Product GUID |
| ProductUnitId | uniqueidentifier | No | Unit GUID |
| SourceInventoryBatchId | uniqueidentifier | No | Source Branch batch GUID; no cross-db FK |
| RequestedQuantity | decimal(18,3) | No | Positive quantity |
| ApprovedQuantity | decimal(18,3) | No | Central-approved dispatch quantity |
| DispatchedQuantity | decimal(18,3) | Yes | Actual dispatch quantity |
| ReceivedQuantity | decimal(18,3) | Yes | Actual destination receipt quantity |
| UnitCostSnapshot | decimal(18,3) | No | Original source batch cost |
| DifferenceQuantity | decimal(18,3) | Yes | Dispatched minus received |

**`StockTransferEvents`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StockTransferEventId | uniqueidentifier | No | PK; sequential Guid |
| StockTransferId | uniqueidentifier | No | FK → StockTransfers |
| EventType | tinyint | No | Requested / Approved / Rejected / DispatchAuthorized / Dispatched / ReceiptAuthorized / Received / DifferenceRecorded / Closed |
| ActorUserId | uniqueidentifier | No | Central employee |
| OccurredAtUtc | datetime2(3) | No | UTC |
| Reason | nvarchar(1000) | Yes | Reason/evidence |

### 8.2 `inventory.StockTransferBranchEvents` — Branch facts; Central synchronized history

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| StockTransferBranchEventId | uniqueidentifier | No | PK; sequential Guid |
| StockTransferId | uniqueidentifier | No | Central transfer GUID; no cross-db FK |
| StockTransferLineId | uniqueidentifier | No | Central line GUID; no cross-db FK |
| BranchEventType | tinyint | No | DispatchApplied / ReceiptApplied / DifferenceObserved |
| InventoryMovementId | uniqueidentifier | No | Local Branch movement GUID |
| OccurredAtUtc | datetime2(3) | No | UTC |
| OperationId | uniqueidentifier | No | Idempotency key |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Transfer → lines/events is 1:N.
- Source and destination branches must differ.
- Branch employees may submit a transfer request; only Central approves and executes the physical dispatch and physical receipt.
- At Central-recorded dispatch, the source Branch receives an authorized command and deducts the dispatched quantity from its stock ledger.
- Destination stock is not available while in transit. At Central-recorded physical receipt, destination Branch receives an authorized command and adds only the actually received quantity.
- Differences between dispatched and received quantities are recorded for Central review; unreceived quantities never become destination available stock.
- Each Branch records only its own physical stock movements and acknowledgement. There is no cross-branch or cross-database FK or distributed transaction.

## 9. Quarantine, disposal and reorder policies

### 9.1 `inventory.InventoryQuarantines` — Branch authoritative; Central projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InventoryQuarantineId | uniqueidentifier | No | PK; sequential Guid |
| InventoryBatchId | uniqueidentifier | No | FK → local InventoryBatches |
| WarehouseLocationId | uniqueidentifier | No | FK → local WarehouseLocations |
| Quantity | decimal(18,3) | No | Positive quarantined quantity |
| QuarantineReason | tinyint | No | Damaged / Expired / NearExpiryHold / Other |
| ReasonDescription | nvarchar(500) | Yes | Required for Other |
| RecordedByEmployeeId | uniqueidentifier | No | Branch employee |
| RecordedAtUtc | datetime2(3) | No | UTC |
| QuarantineStatus | tinyint | No | Active / ReleasedToSellable / DisposalRequested / Disposed |
| RowVersion | rowversion | No | Concurrency token |

### 9.2 `inventory.InventoryDisposalRequests`, `InventoryDisposalRequestLines` — Branch origin; Central approval

**`InventoryDisposalRequests`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InventoryDisposalRequestId | uniqueidentifier | No | PK; generated at Branch and preserved centrally |
| SourceBranchId | uniqueidentifier | No | Branch identity |
| RequestedByEmployeeId | uniqueidentifier | No | Branch employee |
| RequestedAtUtc | datetime2(3) | No | UTC |
| RequestStatus | tinyint | No | Submitted / UnderReview / Approved / Rejected |
| Reason | nvarchar(1000) | No | Request reason |
| RowVersion | rowversion | No | Concurrency token |

**`InventoryDisposalRequestLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InventoryDisposalRequestLineId | uniqueidentifier | No | PK; stable GUID across copies |
| InventoryDisposalRequestId | uniqueidentifier | No | FK to local request |
| InventoryQuarantineId | uniqueidentifier | No | FK to local quarantine |
| InventoryBatchId | uniqueidentifier | No | FK to local batch |
| RequestedQuantity | decimal(18,3) | No | Positive; cannot exceed quarantined available quantity |

### 9.3 `inventory.InventoryDisposalDecisions` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InventoryDisposalDecisionId | uniqueidentifier | No | PK; sequential Guid |
| InventoryDisposalRequestId | uniqueidentifier | No | Central request GUID |
| Decision | tinyint | No | Approve / Reject |
| ApprovedQuantity | decimal(18,3) | Yes | Required for approval |
| Reason | nvarchar(1000) | No | Central decision reason |
| DecidedByUserId | uniqueidentifier | No | Central actor |
| DecidedAtUtc | datetime2(3) | No | UTC |

### 9.4 `inventory.InventoryDisposals` and `InventoryDisposalLines` — Branch execution; Central synchronized evidence

**`InventoryDisposals`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InventoryDisposalId | uniqueidentifier | No | PK; sequential Guid |
| InventoryDisposalRequestId | uniqueidentifier | No | Central approved request GUID |
| ApprovalDecisionId | uniqueidentifier | No | Central approval GUID; no cross-db FK |
| ExecutedByEmployeeId | uniqueidentifier | No | Branch executor |
| ExecutedAtUtc | datetime2(3) | No | UTC |
| EvidenceReference | nvarchar(500) | Yes | Protected photo/document reference |
| RowVersion | rowversion | No | Concurrency token |

**`InventoryDisposalLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| InventoryDisposalLineId | uniqueidentifier | No | PK; sequential Guid |
| InventoryDisposalId | uniqueidentifier | No | FK → InventoryDisposals |
| InventoryBatchId | uniqueidentifier | No | FK → local InventoryBatches |
| InventoryQuarantineId | uniqueidentifier | No | FK → local InventoryQuarantines |
| DisposedQuantity | decimal(18,3) | No | Positive and not greater than approved quantity |
| UnitCostSnapshot | decimal(18,3) | No | Batch cost snapshot |

**Constraints and indexes**
- Quarantine reduces sellable availability immediately; it does not remove physical quantity from the stock ledger.
- Disposal request → lines is 1:N; Central decision is required before physical destruction.
- Branch records actual destruction after approval, including product, quantity, batch, reason, executor, timestamp and evidence.
- Disposal posts an immutable inventory movement and reduces on-hand quantity only after physical execution is recorded.
- No Branch may self-approve or post an unapproved disposal.

### 9.5 `inventory.ReorderPolicies` and `ExpiryAlertPolicies` — Central; Branch projection

**`ReorderPolicies`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ReorderPolicyId | uniqueidentifier | No | PK; sequential Guid |
| BranchId | uniqueidentifier | No | Central FK → org.Branches |
| ProductId | uniqueidentifier | No | Central product GUID |
| ThresholdQuantity | decimal(18,3) | No | Non-negative threshold |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| IsActive | bit | No | Active policy |
| UpdatedByUserId | uniqueidentifier | No | Central actor |
| RowVersion | rowversion | No | Concurrency token |

**`ExpiryAlertPolicies`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ExpiryAlertPolicyId | uniqueidentifier | No | PK; sequential Guid |
| ProductId | uniqueidentifier | Yes | Null for default policy; otherwise product-specific |
| DaysBeforeExpiry | int | No | Non-negative threshold |
| AlertImportance | tinyint | No | Central alert importance |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| IsActive | bit | No | Current policy |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique effective policy scope for Branch + Product (reorder) and Product/default (expiry).
- Central owns thresholds and publishes approved versions to Branches.
- Branch monitors local stock and emits alert events when quantity reaches/breaches reorder threshold or a tracked batch reaches expiry threshold.
- Central may proactively determine replenishment. Branch stock-increase requests are supported as an additional input; Central is not dependent on them.

### 9.6 `inventory.StockIncreaseRequests` and `StockIncreaseRequestDecisions`

Branch-originated requests are optional inputs; Central proactively monitors thresholds and determines replenishment independently.

- `StockIncreaseRequests`: Guid PK, source BranchId, requested employee, product/unit, requested quantity, reason, status and UTC timestamps. Branch origin copy is synchronized to Central using the same GUID.
- `StockIncreaseRequestDecisions`: Guid PK, Central FK to request, approve/reject, approved quantity, reason, Central actor and decision timestamp.
- No request or approval itself creates inventory. Stock changes only through approved receiving or transfer workflows.

## 10. Requirement traceability

| Approved requirement/decision | Schema support |
|---|---|
| BRS-INV-001–007 | Branch Warehouses, Locations, InventoryBalances and Movement ledger |
| BRS-INV-008–016 / INV-01–INV-04 | InventoryBatches, batch cost, expiry and FIFO/FEFO issue references |
| BRS-INV-017–023 / INV-10 | Central StockTransfers, Branch movement events and source/destination posting |
| BRS-INV-024–031 / INV-07–INV-09, INV-12–INV-22 | Central stocktake scope/count/recount/variance/decision plus Branch scoped locks |
| BRS-INV-032–037 / INV-05–INV-06 | Quarantine, Central disposal approval, Branch disposal execution and expiry alerts |
| BRS-INV-038–042 / INV-11 | ReorderPolicies, alert events, StockIncreaseRequests and Central decisions |
| BRS-PUR-012–018 | Receipt lines create accepted batches with per-batch cost and optional expiry |
| WEB-04 and TD-04 reservation rule | Branch StockReservations and allocations, with atomic exclusion from POS sale |

## 11. Technical risks for review

1. Confirm inventory balance grain: this proposal uses Product + Unit + Location + Batch. Any aggregate product/location view should be a read model, not a competing authority.
2. Confirm whether batch-level reservation is always required or only when a reservation is allocated to a specific picking location; the schema supports optional exact batch allocation.
3. Define the exact atomic SQL technique for simultaneous POS sale and website reservation; TD-04 requires serialization against the same Branch state.
4. Define the recovery procedure for a Branch restart while a stocktake lock is active; a local lock must survive process restart and release only after Central command.
5. Define the precise event/command contract for Central-authorized transfer, stocktake adjustment, excess receiving and disposal.
6. Define how expired stock is automatically excluded at query/transaction time even if an expiry alert has not yet been processed.
7. Define the reconciliation algorithm between InventoryMovements and InventoryBalances, with a non-destructive discrepancy report.
8. Define the scope of location-to-location movement and its posting transaction for BRS-INV-007.
9. Define retention for inventory movement, batch, count, transfer and disposal evidence without deleting financial lineage.

## 12. Approval boundary and next step

TD-18 specifies Inventory & Stock only. It does not create SQL tables, EF Core entities, DDL or migrations. The next detailed group is Pricing & Promotions, followed by POS/Sales/Returns, Payments, Online/CRM/Fulfillment, Operations, Alerts, Audit and Reporting.

**No SQL table, EF Core entity, DDL or migration is created by this document.**
