# Feature Specification: Variant availability guard

> Completed on 2026-09-27, after the feature merged (#137), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature Branch**: `054-variant-availability-guard` | **Created**: 2026-09-24 | **Issue**: #124

**Status**: Merged (#137, 2026-09-24)

**Input**: Issue #124 - the variant-level availability guard also requires the value to change, so a newer
announcement repeating the current value does not move the clock, and an older contrary one can win.

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

## User Scenarios & Testing *(mandatory)*

### US1 - The latest observation wins, whatever order they arrive in (Priority: P1)

A shopper sees a variant as in stock or out of stock according to what Inventory most recently observed,
not according to which message happened to arrive last.

**Why this priority**: It is the whole defect. A listing that says "out of stock" for something Inventory last
said was in stock loses a sale; the opposite promises something that cannot be reserved (checkout still
reserves against Inventory, so nothing is oversold - but the shopper is misled).

**Independent Test**: Send the three announcements in the order t1, t3, t2 to Catalog and read the variant and
the product.

**Acceptance Scenarios**:

1. The sequence in stock at t1, in stock at t3, out of stock at t2 (arriving last) leaves the variant,
   and the product's rollup, **in stock**, observed at t3.
2. A duplicate announcement (same time) and an overtaken one (older time) still change nothing.
3. A newer announcement with a new value still wins. That is the control.

### Edge Cases

- **A variant never announced before** (`AvailabilityObservedAt` null): the first announcement is recorded, as
  before.
- **Two announcements with the same observation time.** The second changes nothing (`<`, not `<=`).
- **An announcement for a variant Catalog does not hold.** Zero rows; logged as a Warning and discarded, as
  before.

| Arrives | Observed | Says | Before this feature | After |
| :-- | :-- | :-- | :-- | :-- |
| 1st | t1 | in stock | recorded | recorded |
| 2nd | t3 | in stock | **ignored** (same value), clock stays at t1 | recorded, clock at t3 |
| 3rd | t2 | out of stock | **wins** (t2 > t1): listing says out of stock | loses (t2 < t3) |

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The variant guard compares observation times only, as the product guard does.
- **FR-002**: A same-value newer announcement advances `AvailabilityObservedAt`. It also recomputes the
  product rollup, which is harmless: that recompute is idempotent.

### Key Entities

- **Variant availability**: `product_variants.Availability` and `AvailabilityObservedAt` - Catalog's read model of
  Inventory's word, display only (specs/004). The product's `Availability` is a rollup over its active variants.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The t1 / t3 / t2 sequence leaves the variant in stock observed at t3, verified by a test that failed
  before the fix.
- **SC-002**: The existing duplicate, overtaken and newer-wins tests still pass.

## Assumptions

- Inventory announces only when availability may have changed, so same-value repeats - which now cost one write
  and one rollup - are rare.

## Out of scope

- The product-level guard, which was already right.
