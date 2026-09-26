# Data Model: Cart removal by variant

> Written on 2026-09-27, after the feature merged (#134), from the code at that merge, the pull request and
> docs/features/shopping-and-checkout.md.

**No table, column, index or migration changed** (FR-003). What changed is the shape of the JSON Cart stores in
an existing column.

## `checkout_outcomes` (unchanged schema)

`ecommerce_cart_db`, created by `20260921131735_InitialCartSchema` (specs/010). One row per order, locked
`FOR UPDATE` by each of the three events.

| Column | Type | Note |
| :--- | :--- | :--- |
| `OrderId` | `uuid` | PK |
| `UserId` | `uuid`, null | Known once the submission arrives |
| `ItemsJson` | `jsonb`, null | **Its elements gain `VariantId`** (see below) |
| `Outcome` | `character varying(16)` | `Pending`, `Completed`, `Failed` |
| `Applied` | `boolean` | The once-only guard |
| `UpdatedAt` | `timestamp with time zone` | |

### `ItemsJson` element

Before this feature:

```json
{ "ProductId": "…", "Quantity": 1 }
```

After (the serialised `OrderedItem`; `Sellable` is `[JsonIgnore]` and never stored):

```json
{ "ProductId": "…", "Quantity": 1, "VariantId": "…" }
```

An element written before the change has no `VariantId` and deserialises to `Guid.Empty`, which `Sellable`
reads as the product id - the first variant's id (research D2). Rows for orders already in flight at the
deploy therefore apply correctly without being rewritten.

## `cart_lines` (unchanged)

`VariantId` (`uuid`, null since `20260922044412_AddCartLineVariant`) and `ProductId`; the domain's
`CartLine.SellableId => VariantId ?? ProductId` is what removal now matches.

## Compatibility with an earlier image

An earlier Cart image reading JSON written by this one ignores the extra `VariantId` field and matches by
product, as it always did. Nothing it cannot parse is written.
