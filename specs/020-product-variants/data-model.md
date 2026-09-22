# Data Model: Product Variants

## Catalog

### `product_variants` (new)

| Column | Type | Null | Notes |
| :-- | :-- | :-- | :-- |
| `Id` | `uuid` | no | **For products that existed before this feature, equals the product's id** (research D2). For later variants it does not. Nothing may assume either. |
| `ProductId` | `uuid` | no | FK → `products`, restrict |
| `Sku` | `varchar(50)` | no | Unique across the catalogue |
| `Price` | `decimal(18,2)` | no | What this shape costs |
| `OptionSummary` | `varchar(200)` | no | The options flattened: `Kit: Body only · Colour: Black`. Frozen onto order lines |
| `IsActive` | `bool` | no | Default true. A variant is deactivated, never deleted: orders refer to it |
| `Availability` | `bool` | no | Default false — Inventory's read model, per variant |
| `AvailabilityObservedAt` | `timestamptz` | yes | Null = never announced |
| `CreatedAt` / `UpdatedAt` | `timestamptz` | no | |

### `variant_options` (new)

| Column | Type | Null | Notes |
| :-- | :-- | :-- | :-- |
| `Id` | `uuid` | no | |
| `VariantId` | `uuid` | no | FK → `product_variants`, cascade |
| `Name` | `varchar(50)` | no | `Kit`, `Colour`, `Size` |
| `Value` | `varchar(100)` | no | Free text. `"Black"` and `"black"` are two values (recorded, not solved) |

Unique index on `(VariantId, Name)`: one `Colour` per variant.

### `products` (unchanged columns, new meaning)

`Price` and `Sku` **stay** (research D3). After this feature `Price` holds the cheapest sellable
variant's price and `Sku` the first variant's, both maintained by Catalog. `Availability` stays and is
recomputed as "any variant available".

### Migration `AddProductVariants`

1. Create both tables.
2. `INSERT INTO product_variants (Id, ProductId, Sku, Price, OptionSummary, IsActive, Availability, AvailabilityObservedAt, …)
   SELECT "Id", "Id", "Sku", "Price", '', "IsActive", "Availability", "AvailabilityObservedAt", … FROM products;`
   — one variant per product, **id reused**, no options (there is nothing to choose between).
3. Nothing is dropped, nothing is narrowed, no column becomes `NOT NULL` without a default.

## Inventory — column names unchanged, contents now a variant id

`stock_items.ProductId` and `stock_reservations.ProductId` hold **variant** ids from now on (research
D9). Existing rows are already correct because of the id reuse. No migration.

## Cart

`cart_lines` gains `VariantId uuid` (nullable, additive). Written on every new line; a line written by
an earlier image has it null, and is read as `ProductId` (which is that product's only variant).
`ProductId` stays: it is what the storefront links to.

## Order

`order_items` gains, all nullable and additive:

| Column | Type | Notes |
| :-- | :-- | :-- |
| `VariantId` | `uuid` | Null on lines written before this feature — those are the product's only variant |
| `Sku` | `varchar(50)` | Frozen |
| `OptionSummary` | `varchar(200)` | Frozen — what the customer chose, in words |

`ProductId` and `ProductName` stay and keep their meaning.

## What derives from what

```text
variant.Availability   <- StockAvailabilityChangedEvent (per variant, from Inventory)
product.Availability   <- any(variant.Availability and variant.IsActive)
product.Price          <- min(variant.Price where IsActive)   ("from" price)
variant.OptionSummary  <- its variant_options rows, in insertion order
order_item.*           <- frozen copies, never read back from the catalogue
```
