# Feature Specification: Variant availability guard

**Feature Branch**: `054-variant-availability-guard` | **Created**: 2026-09-24 | **Issue**: #124

## Why

Catalog keeps a read model of whether each variant is in stock, fed by Inventory's
`StockAvailabilityChangedEvent`. The broker redelivers messages and lets them overtake each other, so
every write is guarded: an announcement is recorded only if it was observed **after** the last one
recorded (specs/004).

The variant-level guard added a second condition: `v.Availability != isAvailable`, meaning the value must
change. So a newer announcement that repeats the current value is not recorded, and
`AvailabilityObservedAt` stays at the older time. An announcement older than that repeat, but newer than
the recorded time, then wins when it arrives late.

For example:
- in stock at 10:00;
- in stock again at 10:02;
- out of stock at 10:01, delivered last.

The listing ends up saying out of stock, against what Inventory most recently said. The interface's own
comment says the variant version is "guarded exactly as the product-level version is". The product-level
guard compares timestamps only.

## User Scenarios

### US1 - The latest observation wins, whatever order they arrive in (P1)

**Acceptance**
1. The sequence in stock at t1, in stock at t3, out of stock at t2 (arriving last) leaves the variant,
   and the product's rollup, **in stock**, observed at t3.
2. A duplicate announcement (same time) and an overtaken one (older time) still change nothing.
3. A newer announcement with a new value still wins. That is the control.

## Requirements

- **FR-001**: The variant guard compares observation times only, as the product guard does.
- **FR-002**: A same-value newer announcement advances `AvailabilityObservedAt`. It also recomputes the
  product rollup, which is harmless: that recompute is idempotent.

## Out of scope

- The product-level guard, which was already right.
