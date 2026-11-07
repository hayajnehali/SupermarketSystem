# TD-26 — SQL Server Physical Schema and EF Core Mapping Standards

**Version:** 0.1  
**Status:** Proposed for owner review  
**Date:** 2026-11-07  
**Depends on:** Approved TD-01–TD-12 and the logical database package TD-13–TD-25.  
**Technology baseline:** .NET 10 / ASP.NET Core 10, EF Core 10, SQL Server 2025.

> This document defines the physical implementation standards to apply when converting the approved logical schema into SQL Server tables and EF Core mappings. It does not create DDL, entities, migrations or application code.

## 1. Physical database topology

- One authoritative Central SQL Server database.
- One independent SQL Server database per Branch.
- Central and each Branch database are separate physical databases; no cross-database SQL FKs or distributed SQL transactions.
- Branch APIs connect only to their own Branch database. Central APIs/workers connect only to Central. Cross-site communication uses the approved API/command/Outbox/Inbox contracts.
- Database schemas follow the module names already used in TD-13: `org`, `catalog`, `procurement`, `inventory`, `pricing`, `sales`, `payments`, `online`, `fulfillment`, `crm`, `operations`, `maintenance`, `alerts`, `audit`, `reporting`, `sync`.
- A Branch database contains only the tables assigned to Branch in TD-13 plus the five applicable synchronization infrastructure tables. A Central database contains centrally owned tables and synchronized Branch projections as specified in TD-13.
- SQL Server 2025 is the approved engine. SQL Server edition and sizing remain selected per environment/Branch under TD-08.

## 2. Naming conventions

| Object | Convention | Example |
|---|---|---|
| Database | `Supermarket_Central`, `Supermarket_Branch_{BranchCode}` | `Supermarket_Branch_BR01` |
| Schema | Lowercase module name | `inventory` |
| Table | PascalCase plural | `InventoryBatches` |
| Column | PascalCase | `InventoryBatchId` |
| PK constraint | `PK_{Table}` | `PK_InventoryBatches` |
| FK constraint | `FK_{Dependent}_{Principal}` | `FK_InventoryBatches_Warehouses` |
| Unique index | `UX_{Table}_{Columns}` | `UX_ProductPrices_Version_Product_Unit` |
| Non-unique index | `IX_{Table}_{Columns}` | `IX_InventoryBatches_Product_Expiry` |
| Check constraint | `CK_{Table}_{Rule}` | `CK_InventoryBatches_OriginalQuantity_Positive` |
| Default constraint | `DF_{Table}_{Column}` | `DF_Employees_CreatedAtUtc` |

Use stable names; do not let EF Core generate opaque convention names for production constraints/indexes.

## 3. Primary and foreign keys

### 3.1 Primary keys

- Every table PK is C# `Guid` and SQL Server `uniqueidentifier`.
- Integer/`bigint` identity PKs are prohibited.
- Generate GUIDs client-side with EF Core 10 SQL Server sequential Guid generation so an ID exists before persistence and can be used in Outbox/Inbox messages and cross-site correlations.
- Do not use database `IDENTITY` for any PK.
- Do not use a server-only `NEWSEQUENTIALID()` default as the application identity generator; the application must know the ID before committing related rows/messages.
- PK indexes are clustered by default where appropriate; sequential Guid generation is intended to reduce random insert fragmentation. Any deliberate non-clustered PK exception must be documented with a measured workload reason.

### 3.2 Foreign keys

- All FK columns use `uniqueidentifier` and match the referenced Guid PK.
- SQL FKs exist only between tables in the same database.
- Cross-site references use GUID columns with no SQL FK, plus API contract validation and durable synchronization correlation.
- Composite FKs are allowed when they enforce same-parent consistency, e.g. ProductId + ProductUnitId.
- FKs should be indexed where they participate in joins, filters or delete/update checks; do not blindly add redundant indexes when a unique/composite index already covers the leading columns.
- Default delete behavior is `NO ACTION` / Restrict. Do not cascade-delete financial, inventory, customer-history, audit, synchronization or workflow history.
- Master records are deactivated/status-changed rather than physically deleted after use.

## 4. SQL data types and precision

| Logical data | SQL Server type | Rule |
|---|---|---|
| PK/FK/GUID correlation | `uniqueidentifier` | All PK/FK and cross-site entity references |
| UTC instant | `datetime2(3)` | Store UTC; do not use `datetime` or local server time |
| Business date/expiry/due date | `date` | Date without time-zone semantics |
| JOD monetary amount/unit price/cost | `decimal(18,3)` | Internal currency precision; final invoice amount rounded to two decimals under approved rule |
| Quantity/weight | `decimal(18,3)` | Maximum three decimal places as approved |
| Unit conversion factor | `decimal(18,6)` | Positive conversion multiplier |
| Discount percentage | `decimal(9,4)` | Percentage; application validates range |
| Counter/version/sequence | `bigint` or `int` | Not a PK |
| Small status/type code | `tinyint` | Stable code plus SQL CHECK constraint |
| Boolean | `bit` | No string booleans |
| Country code | `char(2)` | ISO alpha-2 |
| Currency code | `char(3)` | ISO currency |
| SHA-256 hash | `binary(32)` | Canonical payload/file hash |
| OTP hash | `varbinary(64)` | One-way verifier only; never plaintext OTP |
| Concurrency token | `rowversion` | SQL Server-generated; never assigned by application |

### 4.1 String sizing and Unicode

- Use `nvarchar` for names, descriptions, addresses and user-facing Arabic/English text.
- Apply the exact max lengths already listed in TD-15–TD-23; do not default every field to `nvarchar(max)`.
- Use `nvarchar(max)` only for intentionally variable structured JSON payloads or long text that cannot be safely bounded.
- JSON payload columns must have SQL `ISJSON` checks where supported and must be validated against a versioned application schema.
- Use normalized/canonical values for phone numbers, barcodes, stable codes and provider references; preserve original display values separately only when needed.

### 4.2 UTC and defaults

- UTC timestamp columns use `datetime2(3)`.
- `CreatedAtUtc` may use a named `SYSUTCDATETIME()` default, converted to the column precision.
- Business-event time supplied by a trusted workflow is stored separately from persistence/receipt time where the schema requires both.
- `UpdatedAtUtc` and actor fields are set consistently by the application persistence pipeline; do not use hidden triggers that rewrite business timestamps.
- `rowversion` is a binary concurrency token, not a timestamp or audit time.

## 5. Rowversion, status and history rules

- Use `rowversion` on mutable master/configuration/workflow rows where optimistic concurrency is required.
- Immutable event/ledger rows do not need an update-oriented concurrency token unless a specific processing lifecycle requires it.
- Every `tinyint` status/type field has a named CHECK constraint for the allowed stable values.
- Status text and translations belong in the application presentation layer, not in database enum values.
- Audit, stock movement, payment attempt, provider event, loyalty transaction, status history and approval-decision records are append-only under ordinary application roles.
- Corrections use explicit reversal/compensating records or new versions; never silently overwrite historical facts.
- Do not add a universal `IsDeleted` column to every table. Use explicit `IsActive`, status and deactivation fields on master data where TD-15–TD-23 specify them.

## 6. Indexing standards

### 6.1 Required index categories

1. PK index on every table.
2. Unique indexes for business identifiers: BranchCode, EmployeeNumber, SKU, SupplierCode, InvoiceNumber, OrderNumber, normalized customer phone, barcode canonical value and other TD-defined unique keys.
3. Unique composite indexes for parent-scoped identifiers: ProductId + UnitCode, SupplierId + ProductId + EffectiveFromUtc, BranchId + ProductId + effective period, etc.
4. Index FK columns used for joins, parent lookups or referential checks.
5. Query indexes matching the actual filters/orderings used by APIs and workers: Status + CreatedAtUtc, BranchId + BusinessDate, ProductId + ExpiryDate, OrderId + Status, ProviderId + ExternalReference.
6. Unique idempotency indexes for OperationId/IdempotencyKey/provider event IDs at the correct processing scope.
7. Filtered unique indexes for conditional uniqueness: one active primary barcode, one active primary media row, one base unit per product, one active Branch pricing snapshot, one active reservation per order where the schema permits history.

### 6.2 Index discipline

- Indexes must support a known query, uniqueness rule, FK or concurrency path.
- Avoid duplicate indexes with identical leading key columns.
- Use INCLUDE columns only after query-plan/IO evidence; do not include large payloads or sensitive data by default.
- Do not index large JSON payloads or free-text descriptions without a defined search requirement.
- Review actual execution plans and Query Store before adding speculative indexes.
- Partitioning is not a first-release assumption; consider only after measured volume and retention needs justify it.

## 7. Check constraints and filtered uniqueness

Use SQL CHECK constraints for row-local invariants wherever possible, including:

- Positive quantities and non-negative amounts/costs.
- Valid date ranges (`EffectiveToUtc > EffectiveFromUtc`).
- Non-negative display/order counters and positive version numbers.
- Percentage and quantity precision bounds.
- Allowed enum/status values.
- JSON validity for JSON-designated payloads.
- Exactly one typed value column for typed ProductAttributes.
- Valid allocation sums/caps where enforceable within one row.

Use filtered unique indexes for conditional uniqueness. Rules requiring aggregate sums, overlap checks, temporal exclusion, cross-row state transitions or cross-database validation must be enforced transactionally in application/SQL procedures and covered by integration tests; do not pretend a simple CHECK constraint can enforce them.

## 8. Concurrency and atomic database operations

| Operation | Required physical protection |
|---|---|
| Invoice sequence allocation | Atomic counter update/return; unique SequenceYear + SequenceNumber and InvoiceNumber |
| Stock reservation vs POS sale | Branch-local atomic conditional update/locking against the same available stock state |
| Batch issue FIFO/FEFO | Transactionally select eligible batches and persist exact issue allocations |
| Inventory balance projection | Update in same Branch transaction as movement/reservation/quarantine |
| Outbox claim | Atomic lease/claim operation from TD-12; no read-then-update race |
| Inbox/idempotency | Unique operation/message key; duplicate returns original outcome |
| Refund cap | Transaction-safe cumulative successful refund check against original paid amount |
| Promotion/coupon limits | Transactional local/central usage check; Offline cross-Branch global limits remain a TD-24 technical gate |
| Stocktake freeze | Durable Branch-scoped locks checked by every stock-changing transaction |
| Stock transfer | Central command authorization plus source/destination Branch local posting and durable acknowledgement |

- Use optimistic concurrency (`rowversion`) for mutable rows and explicit transaction/locking for hot aggregate invariants.
- Default isolation is SQL Server READ COMMITTED unless a specific operation requires stronger serialization or an explicit lock hint.
- Any use of `UPDLOCK`, `HOLDLOCK`, `SERIALIZABLE` or stored procedures must be justified by the operation's concurrency test and documented in its module design.
- No distributed transaction across Central and Branch databases.

## 9. EF Core 10 mapping conventions

- Use EF Core 10 aligned with .NET 10.
- Map tables explicitly to the module schema and plural table name; do not rely on pluralization or inferred schema defaults.
- Configure every PK as Guid with sequential client-side generation; configure FKs explicitly.
- Configure exact string lengths, precision/scale, Unicode behavior, required/optional fields, check constraints, filtered/unique indexes and delete behavior in Fluent API.
- Map SQL `rowversion` with `IsRowVersion()` / concurrency token configuration.
- Use value converters only for stable domain types; do not hide database precision or status semantics in implicit conversions.
- Keep module entity configurations in the owning module's Infrastructure persistence folder.
- Recommended initial context boundary: one Central persistence context and one Branch persistence context per physical database, with module-owned entity configurations and internal DbSet access. If module-level DbContexts are later required, document their shared-transaction and migration orchestration explicitly before introducing them.
- Do not let Angular or application clients access EF Core/SQL Server directly.

## 10. Migration and deployment standards

- Maintain separate migration histories/assemblies for Central DB and Branch DB because their table placement differs.
- Migration source must be reviewed and version-controlled; production migration is an explicit deployment step, not automatic application startup behavior.
- Use `__EFMigrationsHistory` per database/migration assembly; do not add a duplicate custom schema-version table unless a proven deployment requirement needs it.
- Branch databases must converge on the same approved Branch schema version; Central tracks release/migration compatibility before issuing commands that require a newer schema.
- Prefer expand → deploy compatible code → backfill → switch usage → contract migrations. Do not drop/rename a live column in the same deployment that stops supporting it.
- Test every migration against a representative Central dataset and a Branch dataset, including backup/restore and forward-fix procedures.
- Record migration version, release version, deployment time, approver and recovery decision.
- Follow TD-08 backup/RPO/RTO and rollback/forward-fix requirements. Application rollback must never discard committed transactions or reverse an irreversible payment.

## 11. Physical schema implementation sequence

After owner approval of TD-13–TD-25 and resolution of TD-24 implementation-impacting gates:

1. Freeze the logical ERD and table inventory.
2. Finalize Organization/Access physical mappings and identity Guid-key configuration.
3. Finalize Catalog and Pricing physical mappings.
4. Finalize Procurement and Inventory physical mappings.
5. Finalize POS/Sales/Returns and Payments physical mappings.
6. Finalize Online/CRM/Fulfillment physical mappings.
7. Finalize Operations/Maintenance/Alerts/Audit/Reporting physical mappings.
8. Produce Central and Branch EF Core configurations and migration plans.
9. Generate migrations, SQL scripts and integration/concurrency tests.
10. Validate schema deployment and recovery against TD-08.

## 12. Approval boundary

TD-26 establishes proposed SQL Server/EF Core physical conventions. It does not authorize creation of database tables, application entities, DDL or migrations. Those begin only after the logical schema and the implementation-impacting technical decisions are approved.
