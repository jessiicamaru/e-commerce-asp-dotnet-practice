# Specification Quality Checklist: A seller can stock what they sell

**Created**: 2026-09-23 · **Feature**: [spec.md](../spec.md)

## Content Quality

- [X] No implementation details in the spec itself (they are in plan.md and contracts/)
- [X] Focused on user value: a seller who cannot stock cannot sell
- [X] Written for non-technical stakeholders
- [X] All mandatory sections completed

## Requirement Completeness

- [X] No [NEEDS CLARIFICATION] markers remain
- [X] Requirements are testable and unambiguous
- [X] Success criteria are measurable
- [X] Success criteria are technology-agnostic
- [X] All acceptance scenarios are defined
- [X] Edge cases identified: the stock row not existing yet, a concurrent reservation, an
      administrator's own products having no seller
- [X] Scope bounded: no deltas, no quantity on the create form, no stock history
- [X] Dependencies and assumptions identified, including the one that costs autonomy

## Feature Readiness

- [X] All functional requirements have clear acceptance criteria
- [X] User scenarios cover primary flows
- [X] Feature meets measurable outcomes defined in Success Criteria
- [X] No implementation details leak into the specification

## Notes

One requirement in the issue was **withdrawn during research**: whether `PUT` should become a delta
to avoid a lost update. It cannot happen - `SetStockOnHandCommandHandler` already takes the same
`FOR UPDATE` lock as the reserve path and already refuses any value below `QuantityReserved`, and
the command's own summary says absolute is deliberate. Recorded in research D5 rather than carried
as open scope.
