# Specification Quality Checklist: A seller can see what they sold

**Created**: 2026-09-23 · **Feature**: [spec.md](../spec.md)

## Content Quality

- [X] No implementation details in the spec (they are in plan.md, research.md and contracts/)
- [X] Focused on the seller's question: did anything I listed sell
- [X] Written for non-technical stakeholders
- [X] All mandatory sections completed

## Requirement Completeness

- [X] No [NEEDS CLARIFICATION] markers remain
- [X] Requirements are testable and unambiguous
- [X] Success criteria are measurable
- [X] Success criteria are technology-agnostic
- [X] All acceptance scenarios are defined
- [X] Edge cases identified: an order mixing sellers, a product deleted after it sold, orders from
      before the feature, a catalogue that cannot say whose a product is
- [X] Scope bounded: no seller fulfilment, no backfill, no payouts
- [X] Dependencies and assumptions identified

## Feature Readiness

- [X] All functional requirements have clear acceptance criteria
- [X] User scenarios cover primary flows
- [X] Feature meets measurable outcomes
- [X] No implementation details leak into the specification

## Notes

Three decisions were taken in auto mode rather than asked, each recorded in research.md with its
reasoning: the seller is **frozen** (D1, the opposite of specs/031 and why); a seller sees **only their
lines and none of the order's totals, customer or address** (D4); **no backfill** (D7). Seller
fulfilment was left out and filed as its own follow-up rather than folded in.
