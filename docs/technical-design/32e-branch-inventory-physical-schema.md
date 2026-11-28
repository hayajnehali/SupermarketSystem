# TD-32E — Branch Inventory, Batches, Reservations and Stocktake Physical Schema Decisions

**Version:** 0.1  
**Status:** Delegated design approval — approved for physical mapping.  
**Date:** 2026-11-28  
**Depends on:** TD-18, TD-26, TD-30, TD-31 and TD-32.

## 1. Authority

Each Branch database is authoritative only for that Branch's physical stock, batches, balances, movements, reservations, quarantine and execution facts. Central holds synchronized read projections and owns approval workflows; it never directly changes Branch stock.

## 2. Inventory grain and batch cost

1. The operational balance grain is exactly Product + ProductUnit + WarehouseLocation + InventoryBatch.
2. `InventoryBatches.OriginalQuantity` and `UnitCost` are immutable after receipt/transfer posting. Corrections use compensating stock/cost records.
3. `InventoryBalances` is a rebuildable operational projection; `InventoryMovements` is the append-only stock ledger.
4. No weighted-average costing is used. COGS and supplier return values use the actual cost of the exact batch quantities involved.
5. Batch issue order is deterministic:
   - Expiry-tracked product: FEFO by ExpiryDate ascending, then ReceivedAtUtc, then InventoryBatchId.
   - Non-expiry product: FIFO by ReceivedAtUtc ascending, then InventoryBatchId.
6. A batch is unavailable when quarantined, depleted, closed, or past its expiry date. A product's expiry date remains sellable through that Branch's local expiry date; it becomes expired on the following local business date. The Branch time zone comes from its approved Central Branch configuration.
7. A product requiring expiry tracking cannot receive/post a batch without ExpiryDate.

## 3. Atomic inventory balance and movement rules

- Every stock-changing operation checks durable stocktake locks and sellability inside the same Branch transaction.
- A stock issue/reservation must atomically verify `OnHandQuantity - ReservedQuantity - QuarantinedQuantity - OtherUnavailableQuantity >= requested quantity`; negative stock is impossible.
- Use optimistic concurrency (`rowversion`) plus conditional SQL updates. A failed rowversion/quantity predicate causes reload and full business re-evaluation; it is not blindly retried.
- The sale/reservation, exact batch allocations, InventoryMovement header/lines, balance updates and Outbox event commit in one Branch transaction.
- A multi-batch operation is all-or-nothing. If any required batch allocation fails, roll back the entire operation and re-evaluate availability.
- Movement headers and lines are immutable ledger facts. They do not need rowversion; corrections create compensating movements. Mutable balances and batch status use rowversion.
- Use named unique keys on the source business operation/OperationId for effects that must post once. Do not impose a blanket unique OperationId on all movements because one operation may legitimately create multiple movement lines/headers by defined purpose.
- Location-to-location transfer posts source and destination balance effects plus one linked movement in one local transaction.

## 4. Stock reservation policy

1. Every accepted website stock reservation is allocated to exact batch/location rows at acceptance time using the product's FEFO/FIFO rule.
2. `StockReservationAllocations` is required for every Accepted reservation; Pending/Rejected reservations may have no allocations.
3. One reservation may span multiple batches and locations. The sum of active allocation quantities must equal the reservation quantity for an Accepted reservation.
4. Acceptance updates ReservedQuantity for all allocated balance rows and writes reservation/allocation records in one Branch transaction before Central acknowledgement.
5. POS sales and website reservations update the same InventoryBalances rows with conditional concurrency checks; neither can consume stock already reserved by the other.
6. The Central 15-minute checkout/payment lifecycle is authoritative. Branch does not release an accepted reservation based on its own clock. It releases only after a valid Central release/cancel/expiry command or the approved fulfillment completion command.
7. Duplicate reservation requests/commands return the original result by OnlineOrderReservationId/OperationId; they cannot reserve twice.
8. If a reserved batch becomes expired, quarantined or otherwise unavailable before picking, the Branch reports a fulfillment exception. It cannot silently substitute another product or alter the order; any reallocation follows the approved Central workflow and is durably recorded.

## 5. Stocktake freeze and recovery

- Central creates stocktake scope and controls counting/recount/investigation/closure.
- Branch applies durable `inventory.StocktakeLocks` for the exact product/location scope and acknowledges the freeze only after the lock is committed.
- Every stock-changing transaction checks for an overlapping active lock inside its transaction. UI checks alone are insufficient.
- Locks survive worker/API restart and database connection loss. They are not released by timeout or process restart.
- Only an authenticated Central completion/cancellation command releases the scoped lock; duplicate commands are idempotent.
- If Branch cannot confirm lock release or the acknowledgement is ambiguous, Central keeps the stocktake in a non-closed recovery state until the Branch's durable state is reconciled.

## 6. Quarantine, expiry and disposal

- Quarantine moves quantities from available to quarantined state without deleting or rewriting the batch or prior movement history.
- A quarantined quantity cannot be sold or reserved.
- Release from quarantine is a separately authorized stock movement and requires inspection/outcome evidence.
- Disposal requires Central approval and then Branch physical execution. InventoryDisposal and its lines create the stock issue movement atomically at execution.
- Expired quantities are excluded at transaction time based on Branch local business date even if an expiry alert has not yet been generated.
- Disposal and supplier-return quantity caps are checked against the same batch's remaining non-reserved/non-quarantined available quantity in a transaction.

## 7. Branch inventory physical indexes and constraints

- Unique WarehouseCode within Branch; unique WarehouseId + LocationCode.
- At most one active default Warehouse.
- Unique Product + Unit + Location + Batch balance row.
- All balance quantities are non-negative and unavailable components cannot exceed OnHandQuantity.
- Positive original batch quantity, positive movement-line quantity and non-negative batch/unit costs.
- Local FKs from balances to location/batch; from batches to local warehouse and local product/unit projections where those projections exist.
- SourcePurchaseReceiptLineId, SourceStockTransferLineId and all Central order/reservation/command IDs are correlation GUIDs unless a local authoritative copy exists.
- Filtered/conditional unique keys prevent duplicate reservation acceptance and duplicate source movement posting.
- Stocktake lock scope indexes support product/location overlap checks; use a normalized lock scope model and transactionally enforce overlap.
- Immutable movement rows are append-only; mutable batch/balance rows use rowversion.

## 8. Reconciliation and recovery

- Reconciliation recomputes balances from immutable movements plus active reservation/quarantine state and reports differences before any correction.
- Automated reconciliation does not silently overwrite ledger history or force a balance adjustment; correction is a controlled compensating movement.
- A restart resumes pending commands from durable Inbox/ProcessedOperations and the persisted reservation/stocktake state.
- Central reporting must display last synchronization freshness and cannot represent a stale Branch projection as current.

## 9. Required integration/concurrency tests

- Two concurrent POS sales against the same batch; two concurrent website reservations; POS sale racing website reservation.
- Multi-batch FEFO/FIFO allocation and deterministic tie-breaks.
- Reservation release/consume duplicate command replay.
- Expiry boundary in Branch local time zone and daylight-saving transitions.
- Quarantine/disposal/return racing sale or reservation.
- Stocktake lock racing receipt, sale, transfer, reservation, disposal and adjustment.
- Worker/API restart while stocktake lock and reservations are active.
- Rebuild InventoryBalances from ledger and compare with operational projection.
- No test may use EF Core InMemory as a substitute for SQL Server concurrency behavior.

## 10. Approval record

The owner delegated detailed design decisions on 2026-11-28. This Branch inventory physical design is approved for mapping. Exact SQL conditional-update statements and lock-overlap queries must be implemented and proven with SQL Server 2025 integration tests before production use.
