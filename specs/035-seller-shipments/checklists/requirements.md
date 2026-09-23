# Specification Quality Checklist: Each seller ships their own part

**Created**: 2026-09-23 · **Feature**: [spec.md](../spec.md)

- [X] No implementation details in the spec (plan, research and contracts hold them)
- [X] Requirements testable and unambiguous; success criteria measurable
- [X] Edge cases: concurrent shipping, orders from before, orders written by a rolled-back image
- [X] Scope bounded: no split delivery charge, no shop names on parcels, no cancellations
- [X] No [NEEDS CLARIFICATION] markers

## Notes

Four decisions the issue left open were taken in auto mode and recorded in research.md: one model
with the shop's goods as a part (D2); `orders.Status` kept as a summary in its existing values, no
"partly shipped" enum member, because a rolled-back image could not parse it (D4); parts created on
demand as well as at checkout, because a rolled-back image writes orders without them (D3); the
delivery charge is not split (D8).
