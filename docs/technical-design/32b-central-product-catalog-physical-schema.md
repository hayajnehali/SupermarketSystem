# TD-32B — Central Product Catalog Physical Schema Decisions

**Version:** 0.1  
**Status:** Delegated design approval — approved for physical mapping.  
**Date:** 2026-11-28  
**Depends on:** TD-16, TD-26, TD-27, TD-28, TD-30 and TD-31.

## 1. Authority and scope

This document resolves the Product Catalog technical risks in TD-16 using the design authority delegated by the project owner. It supplements TD-16; it does not replace its detailed table definitions. Central remains the sole catalogue authority. Branch catalogues are read-only, versioned projections.

## 2. Approved physical decisions

### 2.1 Product identity and SKU
- SKU is globally unique across the company, centrally assigned, immutable after activation/operational use, and stored as `nvarchar(60)`.
- A different size/type that is sold as a distinct product receives a distinct ProductId and SKU.
- ProductId is the stable relational identity; SKU is the immutable business identifier.

### 2.2 Barcode normalization and uniqueness
- Store both the submitted/display value and a canonical value only where preserving the submitted representation is operationally useful; the canonical value is the uniqueness/search key.
- Trim surrounding whitespace. Never silently remove leading zeros.
- Apply symbology-specific validation. For GS1 GTIN formats, validate permitted length and check digit before activation. Do not apply one universal transformation to all barcode symbologies.
- Store BarcodeType with the canonical value; the same active canonical retail barcode cannot identify two products. A barcode can be deactivated but historical references remain.
- Enforce a filtered unique index on canonical active barcode. One product can have multiple active barcodes, but at most one active primary barcode.

### 2.3 Product units and conversions
- Supported UnitKind codes are Piece=1, Weight=2, Volume=3, Length=4, Package=5.
- Every active product has exactly one inventory base unit. A draft product may be incomplete until activation.
- Multiple selling units per product are allowed; each sellable unit has its own Pricing record.
- Open-pack behavior is explicit per unit and validated against conversion rules. Fractional quantities are allowed only when unit configuration permits them; maximum quantity precision remains three decimal places.
- Conversion factors are positive decimal(18,6), directional and effective-dated. Do not infer or store inverse conversions automatically.
- Changes to units/conversions that have historical stock or sales impact require controlled effective-dated changes; they must not rewrite historical transaction quantities.

### 2.4 Product attributes
- ValueType codes: Text=1, Number=2, Boolean=3, Date=4.
- `ProductAttributes` contains exactly one populated typed value column matching the definition's ValueType.
- Numeric attribute values use decimal(18,6); UnitLabel is metadata, not a conversion engine. Inventory/selling unit conversions remain ProductUnits/Conversions.
- Category-scoped definitions are validated against the product category. Category changes are audited and must revalidate affected attributes without silently deleting their historical audit trail.

### 2.5 Product media
- SQL stores metadata and a protected object-storage key, not image bytes and not arbitrary public URLs.
- Media metadata retains content type, size in bytes, SHA-256 hash, original filename where needed, display order, primary marker and active status.
- Only approved image formats are accepted by the upload service; exact byte-size and dimension limits are configuration constants in the media service and must be set before production rollout.
- One active primary media record per product; media replacement/deactivation does not erase historical audit metadata.

### 2.6 Product requests
- One Branch-originated ProductRequest may contain multiple ProductRequestLines.
- The same request and line GUIDs are retained in the Central review copy.
- Branch request payloads are immutable after submission except through explicit correction/resubmission workflow; Central decisions are append-only.
- Approval of a request does not create or activate a Product automatically. Central creates the Product, assigns the SKU, configures units/barcodes/attributes, and sets branch availability.

### 2.7 Composite products
- Composite products may contain other products as components.
- Direct self-reference and indirect cycles are prohibited.
- Component quantities are positive decimal(18,3), component units must belong to the component product, and composition is effective-dated.
- No arbitrary executable recipe/script is stored. Component issue and actual batch costs are captured at sale time.
- A maximum nesting depth is not imposed as a business rule in this release; the application must validate cycles and enforce a conservative implementation guard against pathological traversal.

### 2.8 Category history
- Category changes are retained through the append-only AuditEntries/AuditChangeDetails mechanism with actor, time, target and before/after category IDs.
- No separate ProductCategoryHistory table is introduced unless reporting requirements later demonstrate a distinct query/performance need.

## 3. Physical constraints and indexes to implement

- Unique `catalog.Products.SKU`.
- Unique `ProductCategories.CategoryCode`, `Brands.BrandCode`, and `Countries.CountryCode`.
- Filtered unique active canonical barcode and filtered unique active primary barcode per product.
- Unique `ProductUnits(ProductId, UnitCode)`; filtered unique inventory base unit per ProductId.
- Unique conversion tuple `(ProductId, FromProductUnitId, ToProductUnitId, EffectiveFromUtc)`.
- Composite FK `(ProductId, ProductUnitId)` for product barcodes where a unit-specific barcode is set; matching alternate key on ProductUnits.
- Composite FK `(ComponentProductId, ComponentProductUnitId)` to ensure the component unit belongs to that product.
- Typed attribute CHECK requires exactly one of TextValue, NumberValue, BooleanValue, DateValue; ValueType match is additionally validated in the application because SQL CHECK cannot read another row's definition.
- Effective-date overlap rules for product/branch availability and unit conversions are enforced transactionally in the Application layer; simple row-local CHECK constraints are insufficient.
- All FKs are Central-local in Central and Branch-local in the projection database. No cross-database FK is created.

## 4. Implementation mapping boundary

- Central owns write models for every catalogue table.
- Branch catalogue projection rows are applied only from authenticated, versioned Central publications. Branch APIs expose no create/update/delete use cases for catalogue master records.
- Product, unit, barcode, attribute and media changes are published as versioned projections. A failed/incomplete projection is not activated.
- The EF Core configuration must explicitly map schema `catalog`, table names, lengths, precision, status CHECK constraints, filtered unique indexes and rowversion.
- Integration tests must cover SKU/barcode uniqueness, barcode leading-zero preservation, base-unit uniqueness, conversion validity, composite cycles, attribute typed-value validation and atomic publication/apply.

## 5. Approval record

The owner delegated detailed design decisions on 2026-11-28. The decisions in this supplement are approved for physical schema mapping. They do not by themselves authorize a partial migration before the complete Central and Branch schema packages are reconciled.
