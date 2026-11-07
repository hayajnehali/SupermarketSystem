# TD-16 — Detailed Logical Schema: Product Catalog

**Version:** 0.1  
**Status:** Proposed for owner review  
**Date:** 2026-11-07  
**Scope:** Central product catalogue, categories, brands, origin countries, SKU, barcodes, selling units, conversions, composite definitions, product information/media, branch availability and branch product requests.  
**Depends on:** Approved BRS Unit 02 and decisions PDI-01–PDI-04, UNT-01–UNT-03, BAR-01–BAR-03, PRD-STATUS-01/02, TD-02, TD-03, TD-04, TD-13 and TD-14.

> This is a table-level schema proposal, not executable DDL, EF Core code or a migration. Product pricing remains owned by Pricing & Promotions and is specified separately. No business rule is changed here.

## 1. Shared conventions

- Every PK is application-generated sequential `Guid` / SQL Server `uniqueidentifier`; no integer identity PK.
- All local FKs are `uniqueidentifier` and reference a table in the same database only.
- Central catalogue tables are authoritative; Branch copies are read-only approved projections.
- UTC instants use `datetime2(3)`; calendar-only values use `date`.
- Quantities use `decimal`, never floating-point. Product sale quantity precision is at most three decimal places as approved.
- Mutable central master rows use `rowversion` for optimistic concurrency.
- Deactivation preserves historical references. Do not cascade-delete product master, units, barcodes, attributes, or media metadata after use.
- Product descriptive changes do not rewrite invoice, stock movement, or order snapshots.

## 2. Placement

| Table | Central DB | Each Branch DB |
|---|---|---|
| `catalog.ProductCategories` | Authoritative | Read-only projection |
| `catalog.Brands` | Authoritative | Read-only projection |
| `catalog.Countries` | Authoritative | Read-only projection |
| `catalog.Products` | Authoritative | Read-only projection |
| `catalog.ProductBranchAvailability` | Authoritative | Read-only projection |
| `catalog.ProductBarcodes` | Authoritative | Read-only projection |
| `catalog.ProductUnits` | Authoritative | Read-only projection |
| `catalog.ProductUnitConversions` | Authoritative | Read-only projection |
| `catalog.CompositeProductComponents` | Authoritative | Read-only projection |
| `catalog.ProductAttributeDefinitions` | Authoritative | Read-only projection |
| `catalog.ProductAttributes` | Authoritative | Read-only projection |
| `catalog.ProductMedia` | Authoritative | Read-only projection/media references |
| `catalog.ProductRequests` and `catalog.ProductRequestLines` | Central review copy | Branch-originating request copy |
| `catalog.ProductRequestDecisions` | Authoritative | No |

A request submitted from a Branch is durably recorded with a stable request GUID at its origin and synchronized to Central. The Central copy owns the decision. The two copies are correlated by GUID; there is no cross-database FK.

## 3. Table specifications

### 3.1 `catalog.ProductCategories` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductCategoryId | uniqueidentifier | No | PK; sequential Guid |
| ParentCategoryId | uniqueidentifier | Yes | Self-FK → ProductCategories |
| CategoryCode | nvarchar(50) | No | Unique stable code |
| Name | nvarchar(150) | No | Category name |
| Description | nvarchar(500) | Yes | Optional description |
| DisplayOrder | int | No | Default 0; non-negative |
| IsActive | bit | No | Deactivation only |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor reference |
| UpdatedAtUtc | datetime2(3) | Yes | UTC |
| UpdatedByUserId | uniqueidentifier | Yes | Central actor reference |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- PK clustered on ProductCategoryId.
- Unique index on CategoryCode.
- FK ParentCategoryId → ProductCategories; restrict delete.
- Check DisplayOrder >= 0 and ParentCategoryId <> ProductCategoryId.
- Prevent category cycles in the application/domain validation; a simple self-FK alone cannot prevent deeper cycles.
- Deactivate categories; do not delete categories used by products or historical transactions.

### 3.2 `catalog.Brands` and `catalog.Countries` — Central

**`catalog.Brands`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| BrandId | uniqueidentifier | No | PK; sequential Guid |
| BrandCode | nvarchar(50) | No | Unique stable code |
| Name | nvarchar(150) | No | Brand name |
| IsActive | bit | No | Deactivation only |
| CreatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**`catalog.Countries`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CountryId | uniqueidentifier | No | PK; sequential Guid |
| CountryCode | char(2) | No | Unique ISO 3166-1 alpha-2 code |
| Name | nvarchar(100) | No | Country display name |
| IsActive | bit | No | Reference status |

**Constraints and indexes**
- Unique BrandCode and CountryCode.
- Product brand and country-of-origin references are optional; absence must be supported.
- Countries are reference data and are not physically deleted after use.

### 3.3 `catalog.Products` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductId | uniqueidentifier | No | PK; sequential Guid |
| SKU | nvarchar(60) | No | Unique; distinct size/type is a distinct product/SKU |
| Name | nvarchar(200) | No | Product name |
| Description | nvarchar(2000) | Yes | Optional display description |
| ProductCategoryId | uniqueidentifier | No | FK → ProductCategories |
| BrandId | uniqueidentifier | Yes | FK → Brands |
| CountryOfOriginId | uniqueidentifier | Yes | FK → Countries |
| ProductStatus | tinyint | No | Draft / Active / Inactive; stable constants |
| RequiresExpiryTracking | bit | No | Determines expiry tracking and FEFO policy applicability |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor reference |
| UpdatedAtUtc | datetime2(3) | Yes | UTC |
| UpdatedByUserId | uniqueidentifier | Yes | Central actor reference |
| DeactivatedAtUtc | datetime2(3) | Yes | UTC |
| DeactivatedByUserId | uniqueidentifier | Yes | Central actor reference |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- PK clustered on ProductId; unique index on SKU.
- FKs to category, brand and country; restrict delete.
- Check ProductStatus is a defined value.
- An inactive product cannot be used for new sale, purchase or order operations; historical references remain valid.
- Product name/description edits do not change SKU or historical transaction snapshots.
- Do not add a weighted-average cost field to Products; cost is retained per received batch and issued batch.

### 3.4 `catalog.ProductBranchAvailability` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductBranchAvailabilityId | uniqueidentifier | No | PK; sequential Guid |
| ProductId | uniqueidentifier | No | FK → Products |
| BranchId | uniqueidentifier | No | FK → org.Branches |
| AvailabilityStatus | tinyint | No | Permitted / NotPermitted |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| UpdatedAtUtc | datetime2(3) | No | UTC |
| UpdatedByUserId | uniqueidentifier | No | Central actor reference |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique ProductId + BranchId + EffectiveFromUtc.
- FKs to Products and Central org.Branches; restrict delete.
- Check EffectiveToUtc is null or later than EffectiveFromUtc.
- A filtered unique index or equivalent temporal validation must prevent overlapping active effective periods for the same ProductId + BranchId.
- Branch receives the approved versioned projection and cannot change availability.

### 3.5 `catalog.ProductBarcodes` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductBarcodeId | uniqueidentifier | No | PK; sequential Guid |
| ProductId | uniqueidentifier | No | FK → Products |
| ProductUnitId | uniqueidentifier | Yes | Optional FK → ProductUnits when barcode identifies a particular selling/package unit |
| BarcodeValue | nvarchar(64) | No | Canonical normalized barcode value |
| BarcodeType | tinyint | No | Stable barcode format/type code |
| IsPrimary | bit | No | At most one active primary barcode per product |
| IsActive | bit | No | Deactivation preserves history |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor reference |
| DeactivatedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique filtered index on BarcodeValue where IsActive = 1. This prevents the same active barcode being assigned to more than one active product.
- Unique filtered index on ProductId where IsPrimary = 1 and IsActive = 1.
- Optional composite FK (ProductId, ProductUnitId) to ensure the selected unit belongs to the same product; implement an alternate unique key on ProductUnits.
- Barcode is optional at product level; products without barcodes remain searchable by SKU/name.
- Normalize/validate barcode before uniqueness checks. Manual entry and scanned input use the same validation path.

### 3.6 `catalog.ProductUnits` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductUnitId | uniqueidentifier | No | PK; sequential Guid |
| ProductId | uniqueidentifier | No | FK → Products |
| UnitCode | nvarchar(30) | No | Unique within Product |
| UnitName | nvarchar(80) | No | Unit display name |
| UnitKind | tinyint | No | Piece / Weight / Volume / Length / Package or approved stable kinds |
| IsInventoryBaseUnit | bit | No | Exactly one base unit per active product |
| IsSellable | bit | No | Whether POS/site may sell in this unit |
| IsOpenPackAllowed | bit | No | Approved open-pack capability for this unit |
| IsWeightBased | bit | No | Supports fractional weight quantities |
| QuantityPrecision | tinyint | No | 0–3 decimal places; max 3 as approved |
| IsActive | bit | No | Deactivation only |
| CreatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique ProductId + UnitCode.
- Unique filtered index on ProductId where IsInventoryBaseUnit = 1; domain validation ensures at least one base unit before product activation.
- Check QuantityPrecision between 0 and 3.
- Check open-pack/weight flags are compatible with the selected unit configuration; exact allowed combinations are validated in the domain model.
- Product unit prices are not stored here; Pricing owns price history per ProductId + ProductUnitId.
- Fractional quantities are accepted only where the unit configuration permits them.

### 3.7 `catalog.ProductUnitConversions` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductUnitConversionId | uniqueidentifier | No | PK; sequential Guid |
| ProductId | uniqueidentifier | No | FK → Products |
| FromProductUnitId | uniqueidentifier | No | FK → ProductUnits for this Product |
| ToProductUnitId | uniqueidentifier | No | FK → ProductUnits for this Product |
| ConversionFactor | decimal(18,6) | No | Positive multiplier from From unit to To unit |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| IsActive | bit | No | Current use flag |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Composite FKs ensure both units belong to ProductId.
- Check ConversionFactor > 0 and FromProductUnitId <> ToProductUnitId.
- Unique ProductId + FromProductUnitId + ToProductUnitId + EffectiveFromUtc.
- Conversion direction is explicit; do not silently assume the inverse factor is always stored.
- Internal conversion calculations preserve precision and apply the approved quantity rounding policy only at the defined business boundary.

### 3.8 `catalog.CompositeProductComponents` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CompositeProductComponentId | uniqueidentifier | No | PK; sequential Guid |
| CompositeProductId | uniqueidentifier | No | FK → Products |
| ComponentProductId | uniqueidentifier | No | FK → Products |
| ComponentProductUnitId | uniqueidentifier | No | FK → ProductUnits belonging to ComponentProductId |
| ComponentQuantity | decimal(18,3) | No | Positive quantity required for one composite unit |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| IsActive | bit | No | Current composition status |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- CompositeProductId must differ from ComponentProductId.
- Composite FK (ComponentProductId, ComponentProductUnitId) ensures the unit belongs to the component product.
- Check ComponentQuantity > 0.
- Unique CompositeProductId + ComponentProductId + ComponentProductUnitId + EffectiveFromUtc.
- Application validation prevents circular/nested component cycles unless an explicit later design approves them.
- The composite product has one total selling price through Pricing; component costs are calculated from the actual component batches issued during sale and stored with the sale.

### 3.9 `catalog.ProductAttributeDefinitions` and `catalog.ProductAttributes` — Central

**`catalog.ProductAttributeDefinitions`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductAttributeDefinitionId | uniqueidentifier | No | PK; sequential Guid |
| AttributeCode | nvarchar(80) | No | Unique stable code |
| Name | nvarchar(120) | No | Display label |
| ValueType | tinyint | No | Text / Number / Boolean / Date; stable constants |
| UnitLabel | nvarchar(40) | Yes | Display unit where relevant |
| ProductCategoryId | uniqueidentifier | Yes | Optional category scope FK |
| IsSearchable | bit | No | Whether the attribute participates in approved search/filtering |
| IsVisibleOnStore | bit | No | Whether shown in product details |
| DisplayOrder | int | No | Non-negative display order |
| IsActive | bit | No | Deactivation only |
| RowVersion | rowversion | No | Concurrency token |

**`catalog.ProductAttributes`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductAttributeId | uniqueidentifier | No | PK; sequential Guid |
| ProductId | uniqueidentifier | No | FK → Products |
| ProductAttributeDefinitionId | uniqueidentifier | No | FK → ProductAttributeDefinitions |
| TextValue | nvarchar(2000) | Yes | Used for text attributes |
| NumberValue | decimal(18,6) | Yes | Used for numeric attributes/measurements |
| BooleanValue | bit | Yes | Used for boolean attributes |
| DateValue | date | Yes | Used for date attributes |
| DisplayOrder | int | No | Non-negative order for this product |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique AttributeCode.
- Unique ProductId + ProductAttributeDefinitionId.
- Check that exactly one value column is populated and matches the definition ValueType.
- Optional category scope must match the Product's category or an approved inherited category rule.
- Supports optional ingredients, nutritional values, warnings, weight, size and measurements without hardcoding every product-specific field into Products.
- Attribute values are not a replacement for ProductUnits or inventory conversion rules.

### 3.10 `catalog.ProductMedia` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductMediaId | uniqueidentifier | No | PK; sequential Guid |
| ProductId | uniqueidentifier | No | FK → Products |
| StorageKey | nvarchar(500) | No | Protected storage/object key; not a public arbitrary URL |
| MediaType | tinyint | No | Image type code |
| AltText | nvarchar(300) | Yes | Accessible description |
| DisplayOrder | int | No | Non-negative order |
| IsPrimary | bit | No | At most one active primary image per product |
| IsActive | bit | No | Deactivation only |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor reference |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- FK Product; restrict delete.
- Unique filtered index on ProductId where IsPrimary = 1 and IsActive = 1.
- Multiple images per product are supported.
- Store image bytes in managed file/object storage; SQL stores metadata and protected references.
- Only Central Administration manages product media.

### 3.11 `catalog.ProductRequests`, `catalog.ProductRequestLines` and `catalog.ProductRequestDecisions`

**`catalog.ProductRequests`** — same logical request GUID is persisted at Branch origin and in Central after receipt.

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductRequestId | uniqueidentifier | No | PK; sequential Guid generated at origin and preserved centrally |
| SourceBranchId | uniqueidentifier | No | Branch identity; FK only in Central copy to org.Branches |
| RequestedByEmployeeId | uniqueidentifier | No | Employee GUID; Central copy may FK to org.Employees, Branch copy has no cross-db FK |
| SubmittedAtUtc | datetime2(3) | No | UTC |
| RequestStatus | tinyint | No | Submitted / UnderReview / Approved / Rejected / ReturnedForCorrection |
| RequestReason | nvarchar(500) | Yes | Reason |
| ReceivedAtCentralUtc | datetime2(3) | Yes | Central receipt time; null in Branch origin copy |
| RowVersion | rowversion | No | Concurrency token |

**`catalog.ProductRequestLines`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductRequestLineId | uniqueidentifier | No | PK; sequential Guid |
| ProductRequestId | uniqueidentifier | No | FK to local ProductRequests copy |
| ProposedSKU | nvarchar(60) | Yes | Optional proposed SKU; Central validates uniqueness |
| ProposedName | nvarchar(200) | No | Proposed product name |
| ProposedDescription | nvarchar(2000) | Yes | Optional description |
| ProposedCategoryId | uniqueidentifier | Yes | Central category reference; no FK at Branch origin |
| ProposedBrandName | nvarchar(150) | Yes | Text proposal if no central BrandId exists |
| ProposedCountryCode | char(2) | Yes | Proposed origin code |
| ProposedUnitDescription | nvarchar(300) | Yes | Proposed units/packaging |
| ProposedAdditionalInfo | nvarchar(max) | Yes | Structured proposal data validated at Central |

**`catalog.ProductRequestDecisions`** — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ProductRequestDecisionId | uniqueidentifier | No | PK; sequential Guid |
| ProductRequestId | uniqueidentifier | No | FK → Central ProductRequests |
| Decision | tinyint | No | Approve / Reject / ReturnForCorrection |
| Reason | nvarchar(1000) | No | Decision explanation |
| DecidedByUserId | uniqueidentifier | No | Central actor reference |
| DecidedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- ProductRequest header to lines is 1:N; at least one line is required before submission.
- Central decisions are append-only; do not overwrite prior decisions.
- Branch-originated request and Central received copy use the same ProductRequestId and line IDs; no cross-database FK.
- An approved request does not itself create or activate a product. Central creates the actual Product and selects its branch availability after approval.
- Product requests and decisions are audited.

## 4. Pricing and supplier boundaries

- Unit selling price is not stored in `catalog.ProductUnits`. The Pricing module owns `pricing.ProductPrices`, linked to ProductId + ProductUnitId and effective price version.
- Promotion/coupon calculations remain in Pricing & Promotions.
- Supplier-specific purchase prices and supplier item identifiers remain in Procurement. A product may be associated with multiple suppliers.
- Each received batch retains its own actual cost in the Branch inventory batch; the catalogue never stores a weighted-average cost.
- Product changes are published to Branch projections as approved versioned data. A Branch cannot edit the central catalogue.

## 5. Requirement traceability

| Approved requirement/decision | Schema support |
|---|---|
| BRS-PRD-001–007 | Products, categories and ProductBranchAvailability |
| BRS-PRD-008 | ProductRequests, ProductRequestLines and ProductRequestDecisions |
| BRS-PRD-009 | Unique SKU per distinct product/size/type |
| BRS-PRD-010–013 / BAR-01–BAR-03 | ProductBarcodes; multiple optional barcodes and active uniqueness |
| BRS-PRD-014–019 / UNT-01–UNT-03 | ProductUnits and ProductUnitConversions; fractional quantities and up to 3 decimal places |
| BRS-PRD-020–025 | CompositeProductComponents and sale-time component issue snapshots |
| PDI-01–PDI-04 | Product name, image/media, price via Pricing, brand, origin, optional description and product attributes |
| PRD-STATUS-01/02 | ProductStatus; central-only deactivation with historical preservation |
| SUPP-01–SUPP-03 | Supplier/product associations and supplier-specific prices/identifiers in Procurement |

## 6. Technical risks for review

1. Confirm the exact SKU format and whether SKU uniqueness is global; this proposal assumes global uniqueness.
2. Confirm barcode normalization rules for leading zeros, symbologies and check digits; uniqueness is enforced on the canonical stored value.
3. Confirm the supported UnitKind values and how open-pack behavior interacts with package/base unit conversions.
4. Confirm whether one product may have multiple active primary selling units; this proposal assumes exactly one inventory base unit but permits multiple selling units.
5. Confirm the allowed product attribute data types and whether nutritional values need numeric units/precision beyond the generic typed-value model.
6. Confirm product image size/format limits and storage provider separately; no database binary storage is assumed.
7. Confirm the branch request payload retention and whether product requests can contain multiple proposed products in one request; this proposal supports one or more request lines.
8. Confirm the maximum depth/complexity of composite product definitions; cycles are prohibited by this proposal.
9. Confirm whether category changes require an explicit history table beyond the existing audit records.

## 7. Approval boundary and next step

TD-16 specifies Product Catalog only. It does not create database tables or authorize EF Core implementation. After review, the next detailed group is Supplier & Procurement, followed by Inventory, Pricing, POS/Returns, Payments, Online/CRM/Fulfillment, Operations and Reporting.

**No SQL table, EF Core entity, DDL or migration is created by this document.**
