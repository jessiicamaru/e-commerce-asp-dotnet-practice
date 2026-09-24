# Implementation Plan: Variant availability guard

**Branch**: `054-variant-availability-guard` | **Spec**: [spec.md](spec.md) | **Issue**: #124

## Design

Remove `&& (v.Availability != isAvailable || v.AvailabilityObservedAt == null)` from
`ProductRepository.TryRecordVariantAvailabilityAsync`. The remaining guard is
`AvailabilityObservedAt == null || AvailabilityObservedAt < observedAt`, the same as the product-level
`TryRecordAvailabilityAsync`.

**Cost.** A same-value newer announcement now writes one row and recomputes one product's rollup, where
before it wrote nothing. Inventory announces only when availability may have changed, so repeats are
rare. Correctness is worth one statement.

## Constitution check

- III (idempotent messaging): a duplicate still changes zero rows, and so does an overtaken announcement.
  Pass.
- V (evidence): the ordering test fails before the fix, and a mutation check re-adds the clause. Pass.
