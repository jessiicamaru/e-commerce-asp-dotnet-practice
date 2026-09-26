# Specification Quality Checklist: A seller can stock what they sell

**Created**: 2026-09-23 · **Feature**: [spec.md](../spec.md)

> Completed on 2026-09-27, after the feature merged (#71), from the code at that merge, the pull request and
> docs/features/marketplace.md. Every item was re-read against the completed spec; the notes at the end
> say what the backfill added.

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

**Backfill, 2026-09-27**: the edge cases the item above lists were only in research and the checklist;
the spec now carries them in an Edge Cases section, with "Why this priority" and an Independent Test
per story and Given/When/Then acceptance scenarios. One requirement was added from the code, FR-009
(Catalog unreachable is 503, not 404), because the built behaviour and its test exist and the spec did
not say it. "Implementation details in the spec" still holds: the gRPC edge and the controller
attribute stay in plan.md and contracts/, although the spec's "Why it is harder" section names the
Catalog column it depends on, as it did before.
