# Data Model: A seller can stock what they sell

> Written on 2026-09-27, after the feature merged (#71), from the code at that merge, the pull request and
> docs/features/marketplace.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

**No table, column, index or migration changed** in either service. The feature changes who may write an
existing row, not what the row holds. There is therefore nothing here for an earlier image to trip over:
rolling Inventory or Catalog back past #71 only closes the write to sellers again.

---

## What is read — Catalog, `ecommerce_catalog_db`

`ProductRepository.GetVariantOwnersAsync` answers the new gRPC call with a three-column projection, no
navigation loaded and no tracking:

| Source | Column | Meaning here |
| :-- | :-- | :-- |
| `product_variants` | `Id` | the variant asked about |
| `product_variants` | `ProductId` | its product (equal to the variant id for a product's first variant, specs/020) |
| `products` | `SellerId` (uuid, nullable) | the owner; **null means the shop itself** (specs/027) |

The projection is the Application-layer record
`VariantOwnership(Guid VariantId, Guid ProductId, Guid? SellerId)` in Catalog's `IProductRepository.cs`.
A variant id that matches no row is simply absent from the result.

## What is read and written — Inventory, `ecommerce_inventory_db`

`stock_items`, unchanged. Its `ProductId` column holds a **variant** id; specs/020 deliberately did not
rename it.
`SetStockOnHandCommandHandler` still:

1. locks the row with `SELECT ... FOR UPDATE` through `IStockRepository.GetForUpdateAsync`, the same lock
   the reserve path takes;
2. refuses a `QuantityOnHand` below `QuantityReserved` with 409;
3. sets `QuantityOnHand` and `UpdatedAt`, stages the `StockAvailabilityChangedEvent` through
   `StockAvailabilityAnnouncer`, and saves once.

The only change is a step **before** step 1: `StockOwnership.RequireCanStockAsync`, which reads nothing
from Inventory's database.

Inventory's in-process record `VariantOwnership(Guid VariantId, Guid ProductId, Guid? SellerId)` (in
`IProductOwnership.cs`) is a transient value, not an entity. It had to be named `VariantOwnership` rather
than `VariantOwner` because the generated proto message already has that name (tasks T027).

## State

There are no new states. The decision is a three-way outcome, evaluated in this order:

```text
caller is Admin ─────────────────────────────▶ allowed (Catalog not asked)
Catalog unreachable after 3 attempts ────────▶ 503
variant absent, SellerId null, or ≠ caller ──▶ 404 "Product with ID '…' was not found."
owner = caller, stock row missing ───────────▶ 404 "Product '…' is not registered in inventory."
owner = caller, row present ─────────────────▶ existing path: 409 below reserved, else 200
```
