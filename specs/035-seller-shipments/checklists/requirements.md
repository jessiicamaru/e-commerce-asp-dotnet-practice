# Specification Quality Checklist: Each seller ships their own part

**Created**: 2026-09-23 · **Feature**: [spec.md](../spec.md)

> Completed on 2026-09-27, after the feature merged (#79), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. The five items that were here are kept; the rest of the
> Spec Kit checklist, which this file had abbreviated, is filled in below.

- [X] No implementation details in the spec (plan, research and contracts hold them)
- [X] Requirements testable and unambiguous; success criteria measurable
- [X] Edge cases: concurrent shipping, orders from before, orders written by a rolled-back image
- [X] Scope bounded: no split delivery charge, no shop names on parcels, no cancellations
- [X] No [NEEDS CLARIFICATION] markers

## Content Quality

- [X] No implementation details (languages, frameworks, APIs) - the spec speaks of parts, steps and
      parcels; the table, the lock and the routes are in plan.md, research.md and contracts/
- [X] Focused on user value and business needs - a seller who can see a sale and cannot send it
- [X] Written for non-technical stakeholders
- [X] All mandatory sections completed - Assumptions were added in the backfill; the original had none

## Requirement Completeness

- [X] No [NEEDS CLARIFICATION] markers remain
- [X] Requirements are testable and unambiguous - each FR maps to a `ShipmentTests` case
- [X] Success criteria are measurable
- [X] Success criteria are technology-agnostic - SC-004 names `verify-saga.sh` as its check, which is a
      named instrument rather than an implementation detail
- [X] All acceptance scenarios are defined
- [X] Edge cases are identified
- [X] Scope is clearly bounded
- [X] Dependencies and assumptions identified - the seller frozen on the line (specs/034), PostgreSQL 15+

## Feature Readiness

- [X] All functional requirements have clear acceptance criteria
- [X] User scenarios cover primary flows
- [X] Feature meets measurable outcomes defined in Success Criteria - see the pull request's evidence in
      plan.md
- [X] No implementation details leak into the specification

## Notes

Four decisions the issue left open were taken in auto mode and recorded in research.md: one model
with the shop's goods as a part (D2); `orders.Status` kept as a summary in its existing values, no
"partly shipped" enum member, because a rolled-back image could not parse it (D4); parts created on
demand as well as at checkout, because a rolled-back image writes orders without them (D3); the
delivery charge is not split (D8).

**Backfill, 2026-09-27**: the spec gained "Why this priority" and an Independent Test per story,
Given/When/Then scenarios, Key Entities, two edge cases the code handles (a different tracking reference
on a repeat; the staff/seller difference on an unpaid order) and an Assumptions section. No requirement
changed and the code contradicted nothing in the record.
