# Data Model: A Total With Something Behind It

> Written on 2026-09-27, after the feature merged (#32), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One migration in `ecommerce_order_db`: `20260921182829_AddOrderTotals`. Five nullable columns, a
backfill, then three CHECK constraints - in that order. No table was added and no column was dropped,
renamed or narrowed.

---

## `orders` - four new columns

| Column | Type | Null | Meaning |
| :--- | :--- | :--- | :--- |
| `Subtotal` | `numeric(18,2)` | yes | Goods before tax: Σ `UnitPrice × Quantity` over the lines |
| `TaxTotal` | `numeric(18,2)` | yes | Σ per-line tax + tax on delivery, each rounded half away from zero |
| `DiscountTotal` | `numeric(18,2)` | yes | Always 0 in this feature (FR-007) |
| `TaxRate` | `numeric(5,4)` | yes | The rate applied, frozen from `Tax:Rates` / `Tax:DefaultRate` at checkout |

Existing columns that take part in the sum, unchanged: `ShippingPrice numeric(18,2) NULL` (feature
011, the delivery part) and `TotalAmount numeric(18,2) NOT NULL` (the grand total, what the saga
charges).

## `order_items` - one new column

| Column | Type | Null | Meaning |
| :--- | :--- | :--- | :--- |
| `TaxAmount` | `numeric(18,2)` | yes | The tax on this line, as rounded and charged |

---

## Constraints

All three are on `orders`, declared in `OrderConfiguration` with `HasCheckConstraint`:

| Name | SQL | Why |
| :--- | :--- | :--- |
| `CK_orders_parts_sum_to_total` | `"Subtotal" IS NULL OR "Subtotal" + COALESCE("ShippingPrice", 0) + "TaxTotal" - "DiscountTotal" = "TotalAmount"` | FR-002: the database refuses a total whose parts do not add up |
| `CK_orders_no_discount_yet` | `"DiscountTotal" IS NULL OR "DiscountTotal" = 0` | FR-007: no discount can be applied yet |
| `CK_orders_tax_rate_range` | `"TaxRate" IS NULL OR ("TaxRate" >= 0 AND "TaxRate" < 1)` | FR-010, in the database as well as at startup |

Every constraint lets a NULL part through. That is the expand half of expand/contract: an Order image
from before this feature inserts rows with no parts, and those checkouts must keep working during a
rollback (research D4).

---

## Backfill

Run inside the migration, **before** the constraints are added, so they validate against rows that
already satisfy them:

```sql
UPDATE orders
   SET "Subtotal"      = "TotalAmount" - COALESCE("ShippingPrice", 0),
       "TaxTotal"      = 0,
       "DiscountTotal" = 0,
       "TaxRate"       = 0
 WHERE "Subtotal" IS NULL;
UPDATE order_items SET "TaxAmount" = 0 WHERE "TaxAmount" IS NULL;
```

Orders from before this feature were charged no tax, so they get the parts that describe what was
actually charged - never an invented tax (FR-011). On the local database at the time of the pull
request: 70 orders, 0 without parts afterwards, 0 whose parts do not sum.

## Down

`Down` drops the three constraints, then the five columns. It is a real rollback of this migration,
not something a running older image needs: the older image never reads these columns.

---

## What did not change, and why

- **No message field.** `OrderSubmittedEvent.TotalAmount` is the grand total and is what the saga
  charges (research D5).
- **No NOT NULL.** A NOT NULL part would refuse every order an older image writes.
- **No delivery-tax column.** `OrderTotals.Compute` returns `DeliveryTax`, but nothing stores it; it is
  recoverable as `TaxTotal − Σ order_items.TaxAmount`. Why it was left out is not recorded.

## Later history

Recorded so this page is not read as the current schema: specs/022 made the amounts currency-aware
(`orders.Currency`, dong with no minor unit), and specs/069 (vouchers) replaced
`CK_orders_no_discount_yet` with `CK_orders_discount_not_negative` once a discount could exist. The sum
constraint is unchanged (see
[docs/features/shopping-and-checkout.md](../../docs/features/shopping-and-checkout.md), rule 9).
