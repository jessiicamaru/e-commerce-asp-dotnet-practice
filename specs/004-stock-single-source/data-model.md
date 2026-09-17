# Phase 1 Data Model: One Source of Truth for Stock

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-17

**No new entity.** One column leaves `products`, two arrive, and Inventory's schema is untouched.

---

## Inventory — unchanged, and worth saying why

`StockItem` already has the shape this feature needs:

| Column | Type | Notes |
| :--- | :--- | :--- |
| `ProductId` | `uuid`, unique | |
| `QuantityOnHand` | `int` | |
| `QuantityReserved` | `int` | |
| — | — | `QuantityAvailable` is **derived in code**, not stored |

The derived-not-stored choice is load-bearing for this feature and must not be "optimised" into a
column. The entity says why in its own comment: *"a third number would be free to disagree with the
other two."* The announcement carries the derived value, so the same reasoning now reaches Catalog.

**No migration on Inventory.** It gains publish calls, not columns.

---

## Catalog — `products`

### Removed

| Column | Was | Why it goes |
| :--- | :--- | :--- |
| `StockQuantity` | `int`, not null | Assigned once at creation and never written again. It is the defect (issue #4) |

### Added

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Availability` | `boolean`, not null, **default `false`** | What Inventory last said about whether this product can be bought |
| `AvailabilityObservedAt` | `timestamptz`, **nullable** | When Inventory observed it. `NULL` means "never told" |

Both declared through `ProductConfiguration` — Fluent API only, as the convention requires.

**The default matters.** `false` means a product nobody has announced reads as unavailable
(FR-005), and an existing row migrated without an announcement does the same. The two ways to be
wrong are not symmetrical: "unavailable" when it is in stock costs a sale and self-corrects on the
next announcement; "available" when it is gone takes an order that cannot be filled.

**The nullability matters too.** `AvailabilityObservedAt IS NULL` is what lets the very first
announcement for a product win the comparison below without a special case in the handler.

---

## The guarded, time-compared update

The only write to these two columns:

```text
UPDATE products
   SET "Availability"           = @isAvailable,
       "AvailabilityObservedAt" = @observedAt
 WHERE "Id" = @productId
   AND ("AvailabilityObservedAt" IS NULL OR "AvailabilityObservedAt" < @observedAt)
```

Rows affected is the answer:

| Rows | Meaning | Is it an error? |
| :--- | :--- | :--- |
| 1 | Recorded | no |
| 0, product exists | A duplicate, or an announcement overtaken by a newer one | **no** — normal operation |
| 0, product absent | An announcement for a product this catalogue does not hold | **no** — logged at `Warning`, acknowledged, not retried |

The zero-row cases are told apart by one existence check on the zero path, as in feature 003: an
operator reading one indistinguishable log line learns nothing.

**Why the comparison and not just a guard.** Redelivery and reordering are two problems:

| Arrives | Then arrives | Without the comparison | With it |
| :--- | :--- | :--- | :--- |
| `available @10:00:05` | `available @10:00:05` (redelivery) | harmless rewrite | 0 rows |
| `unavailable @10:00:07` | `available @10:00:05` (overtaken) | **listing sells goods that are gone** | 0 rows |
| `available @10:00:05` | `unavailable @10:00:07` | correct | 1 row |

The second line is the one that costs something, and a plain "write if different" guard gets it
wrong.

---

## State

`Availability` is a two-state value, and both are reachable from either:

| From | Trigger | To |
| :--- | :--- | :--- |
| `false` (incl. never announced) | an announcement with `IsAvailable = true` and a newer `ObservedAt` | `true` |
| `true` | an announcement with `IsAvailable = false` and a newer `ObservedAt` | `false` |

No third state. `Unknown` was considered and rejected (research D4): it is not an answer anyone can
act on, and it would render as one of the other two anyway.

---

## Existing rows

The migration drops `StockQuantity` and adds `Availability` defaulted to `false`. **Every existing
product therefore reads as unavailable until Inventory announces it.**

This is a real, visible consequence and is not a bug: the catalogue genuinely does not know. The
next stock movement of any kind announces; `PUT /api/stock/{productId}` on each product is the
manual way to trigger one immediately. Recorded here because "we deployed and the whole catalogue
went out of stock" looks like a failure if nobody wrote down that it was expected.

No backfill is attempted — Catalog cannot read Inventory's database to do one (constitution I), and
asking Inventory over HTTP at migration time would make a schema change depend on a service being
awake.

---

## Transaction rules

- **Inventory side**: the stock change and its announcement commit together — stage the entity,
  publish, then one `SaveChangesAsync`. Six handlers, same shape, no exceptions.
- **Catalog side**: one statement. It does not read the product first and stages nothing in the
  change tracker.
- The inbox participates in the same transaction as the update, so a message is marked consumed only
  if the write it caused commits.
- `GET /api/products` is read-only and takes no transaction — availability is a column on the row it
  already selects, so the listing does not get slower.
