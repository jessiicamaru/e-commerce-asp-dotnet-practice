# Data Model: The Checkout Flow Is Verified End to End

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-21

This feature stores nothing and adds no schema. What it has instead is a set of **observations** and
the assertions made from them, and getting those wrong is how a check ends up passing for the wrong
reason. This document is that model.

---

## Observable state: an order

Seven `OrderStatus` values exist; **three are reachable**. The rest are unreachable by design and
documented as such in [003-order-lifecycle/data-model.md](../003-order-lifecycle/data-model.md).

```text
                    ┌──────────────┐
  POST /api/orders  │  Submitted   │  ← the only state an order is in when the call returns
                    └──────┬───────┘
                           │  saga: reserve → pay → settle
              ┌────────────┴────────────┐
              ▼                         ▼
      ┌──────────────┐          ┌──────────────┐
      │  Completed   │          │    Failed    │
      └──────────────┘          └──────────────┘
         terminal                   terminal
```

| Status | Reachable | What the check does with it |
| :-- | :-- | :-- |
| `Submitted` | yes | keep polling; **at timeout this is the stall signature** |
| `Completed` | yes | terminal — assert the success expectations |
| `Failed` | yes | terminal — assert the compensation expectations |
| `Pending`, `StockReserved`, `Paid`, `Cancelled` | **no** | if ever observed, fail loudly: something changed that this check's assumptions rest on |

That last row matters. The check should not silently tolerate a status it believes impossible; the
whole point is to notice when the system stopped matching its description.

`Failed` is reached by **two** routes — reservation failure and payment rejection — and the same
event carries both. The check asserts the destination, never the route.

---

## Observable state: stock for one product

From `GET /api/stock/{productId}`, which is `[AllowAnonymous]`:

| Field | Meaning | Used for |
| :-- | :-- | :-- |
| `QuantityOnHand` | physically on the shelf | success assertion — this is what a sale reduces |
| `QuantityReserved` | held for orders not yet settled | **the held-stock assertion** |
| `QuantityAvailable` | `OnHand − Reserved` | the customer-facing number |

`QuantityAvailable` is derived, not stored. Asserting on it alone is not enough, and this is the
subtlety the whole feature turns on:

> After a completed order, `QuantityAvailable` falls by the amount ordered — and it also falls by
> that amount while the order is merely *held*. The two are indistinguishable from `Available`.
> **`OnHand` is what separates them.** A completed sale reduces `OnHand`; a hold does not.

The motivating bug lived exactly in that gap: the order said `Completed`, `Available` looked right,
and `Reserved` was still non-zero with `OnHand` untouched. A check reading only `Available` would
have passed.

---

## The two readings

Every quantity assertion is a pair, never a comparison against a constant (FR-008).

```text
before = GET /api/stock/{productId}        ← immediately before POST /api/orders
  ... place order, poll to terminal ...
after  = GET /api/stock/{productId}        ← after the order is terminal, never before
```

**Order matters.** Reading `after` before the order is terminal reads a hold in progress and calls
it a result. Every assertion below is evaluated only once the status is `Completed` or `Failed`.

---

## Assertions

### Scenario A — payment approves

| # | Assertion | Requirement | If it fails |
| :-- | :-- | :-- | :-- |
| A1 | status is `Completed` | FR-003 | the flow reached the wrong outcome |
| A2 | `after.OnHand == before.OnHand − quantity` | FR-004 | the sale did not actually consume stock |
| A3 | `after.Reserved == before.Reserved` | FR-005 | **units still held after completion — the motivating bug** |
| A4 | `after.Available == before.Available − quantity` | FR-004 | follows from A2 and A3; kept because it is the customer-visible number |

A3 is the one that catches the bug that started this. It is written as "reserved is unchanged"
rather than "reserved is zero" because another run's order may legitimately hold units of the same
product at the same time — an absolute zero would make the check depend on nothing else happening.

### Scenario B — payment refuses

| # | Assertion | Requirement | If it fails |
| :-- | :-- | :-- | :-- |
| B1 | status is `Failed` | FR-006 | compensation did not run, or ran to the wrong end |
| B2 | `after.OnHand == before.OnHand` | FR-007 | stock was consumed for an order that was never paid |
| B3 | `after.Reserved == before.Reserved` | FR-007 | **units stranded — held for an order that will never complete** |
| B4 | `after.Available == before.Available` | FR-007 | the shelf is back where it started |

B3 is the quiet one. Stranded units are invisible until the product cannot be sold and nothing
explains why — the expiry sweeper eventually returns them, which means the symptom is *"stock came
back minutes later"*, which nobody reports as a bug.

---

## Setup, and the asynchronous gap inside it

The check creates its own data each run, entirely through public interfaces (D5).

```text
Identity   POST /api/auth/login              admin token
Catalog    POST /api/categories              categoryId
Catalog    POST /api/products                productId
                 │
                 │  ProductCreatedEvent ──► Inventory.ProductCreatedConsumer
                 ▼
Inventory  GET  /api/stock/{productId}       poll until the row EXISTS   ← easy to miss
Inventory  PUT  /api/stock/{productId}       set QuantityOnHand
Identity   POST /api/auth/register + login   customer token
```

The poll is not optional. The stock row does not exist when `POST /api/products` returns — it is
created by a consumer reacting to an event, so there is a real gap. Treating it as synchronous
produces a check that fails in setup, for a reason with nothing to do with checkout, and that reads
exactly like a saga failure.

**A fresh product per run** is what makes the before/after readings trustworthy: nothing else is
ordering it, so a non-zero `Reserved` in `before` can only be this check's own doing.

---

## Entities this feature does not have

No table, no migration, no message contract. It is worth saying explicitly: a new record in
`Ecommerce.Contracts` would state that a service says something it does not say, and nothing here
publishes or consumes anything. The check speaks only HTTP, as a customer does.
