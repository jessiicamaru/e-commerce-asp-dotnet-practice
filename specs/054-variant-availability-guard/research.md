# Research: Variant availability guard

> Written on 2026-09-27, after the feature merged (#137), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-24

---

## D1 - Compare observation times only

**Decision**: The variant guard is `v.Id == variantId && (v.AvailabilityObservedAt == null ||
v.AvailabilityObservedAt < observedAt)`; the value clause is gone.

**Rationale**: The guard exists because the broker redelivers and reorders (specs/004): the question it answers
is "is this newer than what I hold?", and the value has nothing to do with that. Requiring the value to change
meant a newer same-value announcement did not move the clock, so an older contrary one overtaken in flight still
won. The product-level guard and the interface's own comment ("guarded exactly as the product-level version is")
already said times only.

**Alternatives considered**:

- **Keep the value clause to save a write.** Rejected: it is the defect. No other alternative is recorded.

---

## D2 - Accept the extra write and rollup

**Decision**: A same-value newer announcement now writes the variant row and recomputes the product rollup.

**Rationale**: The rollup is one idempotent statement, and Inventory announces only when availability may have
changed, so repeats are rare. "Correctness is worth one statement." No measurement is recorded.

**Alternatives considered**: none recorded.
