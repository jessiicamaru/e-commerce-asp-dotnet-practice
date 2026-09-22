# Research: Product Variants

## D1 - The variant is the sellable unit; the product is what a shopper recognises

**Decision**: `product_variants` carries SKU, price, options and availability. `products` keeps name,
description, category and image, and its `Price`/`Sku` columns stay (see D3).

**Rejected**: fixed `Size`/`Colour` columns on the product — a camera has kits, a shirt has sizes, and
a schema that names them can only ever hold the shapes somebody thought of first.

## D2 - The backfilled variant REUSES the product's id

**Decision**: the migration gives every existing product one variant whose **`Id` is the product's own
id**, with its price and SKU copied.

**Why this is the whole feature's cheapest decision**: every row in the system that names a "sellable
thing" today holds a product id — Inventory's `stock_items.ProductId` and `stock_reservations.ProductId`,
`cart_lines.ProductId`, `order_items.ProductId` — and those rows live in **four different databases**.
Making the backfilled variant's id equal to the product's id means all of them are already keyed by the
right variant. Nothing is rewritten, nothing is migrated across a service boundary, and an older image
that still sends a product id is sending a valid variant id.

**Rejected**: fresh ids plus a `ProductVariantCreatedEvent(ProductId, VariantId, Sku)` that Inventory,
Cart and Order consume to rewrite their rows. That is three cross-service backfills, each of which can
half-finish, in exchange for prettier ids.

**The cost, recorded**: for products that existed before this feature, `variantId == productId`, and
for variants created afterwards it does not. Nothing may assume either way. Written in the entity, in
CLAUDE.md, and in the data model.

## D3 - `products.Price` and `products.Sku` stay

**Decision**: keep both columns, keep writing them from the product's cheapest variant (`Price`) and
from its first variant (`Sku`).

**Why**: the constitution requires a previously released image to keep running. An earlier Catalog
selects `Price` on every product query and prices gRPC calls from it; dropping the column takes that
image down instead of restoring it. This is the **expand** half; contracting is a later release, once
nothing deployed reads them.

## D4 - Options are rows, and the pair is unique per variant

**Decision**: `variant_options(VariantId, Name, Value)` with a unique index on `(VariantId, Name)`, so
a variant cannot have two `Colour`s. A variant also stores `OptionSummary`, the options flattened to
`"Kit: Body only · Colour: Black"`, because that string is what gets frozen onto an order line and
shown in a cart, and recomputing it needs the rows loaded.

**Rejected**: JSON in a column (cannot be indexed or constrained per option); an `options` table shared
across variants (a value is not an entity here).

## D5 - Two variants of one product may not carry identical options

**Decision**: enforced in the handler, not in the database. The database cannot express "the *set* of
this variant's options differs from every sibling's set" without a trigger.

**Recorded**: a concurrent pair of identical variants can therefore slip through. The unique SKU index
still stops the duplicate being sold twice under one number.

## D6 - Pricing over gRPC: a new method, not a changed one

**Decision**: `catalog_pricing.proto` gains `PriceVariants` (variant ids in, priced variants out) and
`DescribeVariants` for the cart's display. `GetPrices` and `DescribeProducts` stay exactly as they are.

**Why**: an older Order image calls `GetPrices` with product ids. For every product that existed, that
id is also its variant's id (D2), so the old call keeps working. Changing the existing messages would
break it.

`PricedVariant` carries `variant_id`, `product_id`, `sku`, `name` (the product's), `option_summary`,
`price` (a decimal as a **string**, as `PricedProduct` already does) and `sellable`.

## D7 - Messages: one new field each, nothing renamed

**Decision**:

- `OrderItemDto` gains `VariantId`. `ProductId` stays.
- `StockAvailabilityChangedEvent` gains `VariantId`. `ProductId` stays.

Both are additive: an older consumer ignores the new field, and a message from an older publisher
arrives with `Guid.Empty`, which the consumer reads as "the variant whose id is the product id" (D2).

**Rejected**: replacing `ProductId` with `VariantId`. MassTransit would deserialise an old message
into a command naming nobody.

## D8 - Availability is recorded per variant, and the product's is derived

**Decision**: Catalog's read model moves to the variant (`product_variants.Availability`,
`AvailabilityObservedAt`), and after each announcement the product's own `Availability` is recomputed
as "any variant available". The product column stays for an older image (D3) and because the listing
reads it.

**Unchanged**: nothing may sell against this read model. Checkout still reserves against Inventory's
row under `FOR UPDATE`.

## D9 - Inventory keeps its column names

**Decision**: Inventory's `StockItem.ProductId` and `StockReservation.ProductId` are **not renamed**;
what they hold is now a variant id, and the entity says so in a comment. `PUT /api/stock/{id}` takes a
variant id.

**Why**: renaming a column is a breaking change for an earlier image, and Inventory does not know what
a variant is — it counts sellable units by id. The rename belongs to the contract phase, if ever.

**The cost, recorded**: a column named `ProductId` that holds a variant id is a trap for the next
reader. The comment and this decision are what stand in for the rename.

## D10 - The storefront makes the choice explicit

**Decision**: the listing shows the lowest sellable price, prefixed "from" when the variants differ.
The product page renders one control per option name, and **Add to cart is disabled until a variant is
chosen**, rather than silently defaulting to the first.

**Why**: defaulting sells somebody a kit they did not choose. Where a product has exactly one variant,
that variant is preselected, because there is no choice to make.
