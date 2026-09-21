# Data Model: Somewhere to Put What You Intend to Buy

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-21

A new service, `Cart`, with its own database `ecommerce_cart_db`. Three tables. Nothing here stores a
price — see [research D5](./research.md).

---

## `carts`

One per customer.

| Column | Type | Notes |
| :-- | :-- | :-- |
| `Id` | `uuid` PK | `Guid.CreateVersion7()` per ADR-001 |
| `UserId` | `uuid` | **unique** — one cart per customer, enforced by the database, not by code |
| `CreatedAt` | `timestamptz` | |
| `UpdatedAt` | `timestamptz` | |

`UserId` comes from the validated token and never from a request. The unique constraint is what makes
"exactly one cart per customer" (FR-001) a fact rather than an intention: two first-ever adds racing
each other cannot create two carts.

## `cart_lines`

| Column | Type | Notes |
| :-- | :-- | :-- |
| `Id` | `uuid` PK | |
| `CartId` | `uuid` FK → `carts` | cascade delete |
| `ProductId` | `uuid` | **unique together with `CartId`** — adding again raises the quantity (FR-003) |
| `Quantity` | `int` | `CHECK (Quantity > 0)` — zero means the line is gone, not that it is empty |
| `AddedAt` | `timestamptz` | display order |

**No price, no name.** Both are fetched from Catalog when the cart is read. A cart that stored them
would hold a copy that goes stale silently and — worse — could be mistaken for something to charge.

## `checkout_outcomes`

The cart's own record of each checkout, built from three events ([research D2, D3](./research.md)).

| Column | Type | Notes |
| :-- | :-- | :-- |
| `OrderId` | `uuid` PK | one row per order, whichever event creates it |
| `UserId` | `uuid` null | known once `OrderSubmittedEvent` arrives |
| `Items` | `jsonb` null | `[{productId, quantity}]`, known once `OrderSubmittedEvent` arrives |
| `Outcome` | `text` | `Pending` \| `Completed` \| `Failed` |
| `Applied` | `bool` | **the guard** — lines are removed once, ever |
| `UpdatedAt` | `timestamptz` | |

### The transition, whichever event comes first

Every consumer: insert the row if absent (`ON CONFLICT DO NOTHING`), take it `FOR UPDATE`, act, commit.

```text
                    ┌─────────────────────────────────┐
  OrderSubmitted ──►│ set UserId, Items               │──┐
                    └─────────────────────────────────┘  │
                    ┌─────────────────────────────────┐  │   if Outcome = Completed
  OrderCompleted ──►│ set Outcome = Completed         │──┼─► AND Items present
                    └─────────────────────────────────┘  │   AND NOT Applied
                    ┌─────────────────────────────────┐  │        │
  OrderFailed    ──►│ set Outcome = Failed            │  │        ▼
                    └─────────────────────────────────┘  │   remove lines,
                                                         │   Applied = true
                                                         └─ same transaction
```

The apply condition is checked by **both** `OrderSubmitted` and `OrderCompleted`, so whichever
arrives second does the work. `Applied` makes every repeat a no-op. `Failed` never applies.

### Removing "what was ordered", precisely

For each ordered `(ProductId, Quantity)`, the matching cart line is **reduced by that quantity**, and
deleted if it reaches zero or below. Not "empty the cart", and not "delete the line":

| Cart when completion applies | Ordered | Result |
| :-- | :-- | :-- |
| X×2 | X×2 | X gone |
| X×5 (raised to 5 during checkout) | X×2 | **X×3 stays** |
| X×2, Y×1 (Y added during checkout) | X×2 | **Y stays** |
| X×1 (lowered during checkout) | X×2 | X gone — never negative |
| (X removed during checkout) | X×2 | nothing to do |

That table is US2 scenario 5 and SC-005, and it is why removal is a decrement.

---

## Two devices, one cart

Two changes to the same cart at once must not silently undo each other.

**Changed during implementation.** The plan was an `xmin` concurrency token with a retry on conflict.
What was built instead is **`SELECT … FOR UPDATE` on the cart row**, taken by every write before it
reads the lines — the same pattern Inventory uses for stock. It serialises writes to one cart
outright, so there is no conflict to detect and no retry loop to get wrong, and first-ever creation is
an `INSERT … ON CONFLICT ("UserId") DO NOTHING` followed by the same lock, so twenty concurrent first
adds still produce one cart. Verified by `Two_first_ever_adds_at_once_make_one_cart`.

---

## What a read returns

A cart read joins the cart's own lines with a **per-product** description from Catalog
(`DescribeProducts`, [research D8](./research.md)):

| Line state | Shown as | Blocks checkout? |
| :-- | :-- | :-- |
| product exists, sellable | name, current price, line total | no |
| product exists, **not** sellable | name, marked *not for sale* | **yes** (FR-011) |
| product **missing** from Catalog | product id, marked *no longer available* | **yes** (FR-011) |
| Catalog unreachable | product id and quantity, price *unavailable* | checkout will 503 anyway |

The cart total is labelled an **estimate** (FR-010). The charge is decided at checkout.
