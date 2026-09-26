# Data Model: Two Price Lists, Not One Price Converted

> Completed on 2026-09-27, after the feature merged (#59), from the code at that merge, the pull request, docs/features/catalog.md and docs/features/shopping-and-checkout.md.

Four migrations, all additive: `20260922090031_AddVariantPrices` (Catalog), `20260922091458_AddOrderCurrency`
(Order), `20260922130728_AddPaymentCurrency` (Payment) and `20260922130744_AddSagaCurrency` (Orchestrator).
An earlier image of each service runs against its new schema.

## Catalog

### `variant_prices` (new)

| Column | Type | Null | Notes |
| :-- | :-- | :-- | :-- |
| `Id` | `uuid` | no | |
| `VariantId` | `uuid` | no | FK → `product_variants`, cascade |
| `Currency` | `varchar(3)` | no | `VND`, `USD` - an ISO 4217 code, not an enum, so a third currency is rows |
| `Amount` | `decimal(18,2)` | no | `>= 0`, enforced by a CHECK constraint |

Unique on `(VariantId, Currency)`. **No row means not sold in that currency** (research D3) - the
absence is the meaning, which is why the column is not nullable.

Exact names (from the migration): PK `PK_variant_prices`; FK `FK_variant_prices_product_variants_VariantId`
`ON DELETE CASCADE`; unique index `IX_variant_prices_VariantId_Currency`; CHECK `CK_variant_prices_amount`
`"Amount" >= 0`; `Amount` is `numeric(18,2)`, `Currency` `character varying(3)`. The database allows `0`;
the command validator refuses it (`Amount > 0`, `A_price_of_zero_is_refused_because_zero_is_a_price`) and
refuses an amount the currency cannot hold (research D8).

### Unchanged, and now meaning "the default currency"

`product_variants.Price` and `products.Price`. They are the `VND` amount, the fallback for the default
currency, and what an image built before this feature reads (research D2).

**The migration seeds the dollar list** from the dong list at 25,000 VND = 1 USD, rounded to two
decimals and floored at `0.01`, on 2026-09-22. That rate is recorded in the migration and nowhere
else, because nothing reads it at runtime: the rows it produced are ordinary rows an administrator is
expected to edit. The floor is there because `0.00` passes the `>= 0` constraint and is a free camera.

**No `VND` rows are seeded**, and none are ever written: `product_variants.Price` *is* the default
currency's price. Writing it twice would create two sources for one number, which is the defect this
table exists to avoid rather than to spread. `PUT .../prices/VND` therefore updates the variant's own
`Price`, and `DELETE .../prices/VND` is refused.

## Order

| Column | Type | Null | Notes |
| :-- | :-- | :-- | :-- |
| `orders.Currency` | `varchar(3)` | yes | The currency the order was placed in. Null on orders placed before this feature, which read as the default |

Every existing amount on an order - `TotalAmount`, `Subtotal`, `ShippingPrice`, `TaxTotal`,
`DiscountTotal`, and each item's `UnitPrice`/`TotalPrice` - is in it. Nothing about their shape or
their CHECK constraint changes.

## Orchestrator (added 2026-09-27 - missing from this page as written)

| Column | Type | Null | Notes |
| :-- | :-- | :-- | :-- |
| `order_state_data.Currency` | `varchar(3)` | yes | Stored when `OrderSubmittedEvent` arrives, because `ProcessPaymentCommand` is published from a later transition, by which time the event is gone. Relayed as `Currency ?? ""` |

## Payment

| Column | Type | Null | Notes |
| :-- | :-- | :-- | :-- |
| `payments.Currency` | `varchar(3)` | yes | The currency of `Amount`. Null on rows written before this feature |

Nullable rather than defaulted, for the same reason as the order: a row written last week recorded an
amount whose currency nobody stated, and writing `VND` into it would be inventing a fact. Reads
present null as the shop's default and say so.

New rows always carry a currency: `ChargeOrderCommandHandler` writes the one the saga sent, or the configured
default when it sent `""` (an in-flight message from before the deploy).

## Configuration

```jsonc
// Order and Catalog both
"Money": {
  "DefaultCurrency": "VND",
  "Supported": [
    { "Code": "VND", "Decimals": 0 },
    { "Code": "USD", "Decimals": 2 }
  ]
}

// Order only - a price per currency, replacing the single `Price`
"Shipping": {
  "Options": [
    { "Code": "standard", "Name": "Standard delivery", "Prices": { "VND": 30000, "USD": 2 } },
    { "Code": "express",  "Name": "Express delivery",  "Prices": { "VND": 60000, "USD": 4 } }
  ]
}
```

A delivery option with no price in the checkout's currency is **not offered** (FR-008) - the same rule
as a variant, applied to the other thing that has a price. Startup refuses a configuration where no
option has a price in the default currency, because that shop cannot take an order at all.

## Resolution at read time

```text
price(variant, currency) = variant_prices[(variant, currency)]?.Amount
                        ?? (currency == default ? variant.Price : NULL)   -- NULL, never converted
sellable(variant, currency) = variant.Sellable AND price(variant, currency) IS NOT NULL
fromPrice(product, currency) = MIN(price(v, currency)) over active variants where it is not null
round(amount, currency) = Math.Round(amount, currency.Decimals, AwayFromZero)
```

## What is deliberately not given a currency

- **Tax rates** - a rate is a ratio, and 10% is 10% in any currency.
- **Anything in Inventory** - it counts units, and a unit has no price.
- **Cart lines** - the cart stores no price at all (specs/010), which is why this feature barely
  touches it: it asks Catalog for a price in a currency, and shows the answer.
