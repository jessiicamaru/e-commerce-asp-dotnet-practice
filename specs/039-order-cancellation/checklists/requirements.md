# Specification Quality Checklist: Cancelling a paid order

**Created**: 2026-09-23 · **Feature**: [spec.md](../spec.md)

> Completed on 2026-09-27, after the feature merged (#84), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. The seven items that were here are kept; the rest of the
> Spec Kit checklist is filled in below.

- [x] No implementation details in the spec
- [x] Focused on user value
- [x] No [NEEDS CLARIFICATION] markers remain (window and partial cancellation decided with the user)
- [x] Requirements testable and unambiguous
- [x] Success criteria measurable
- [x] Edge cases identified (cancel vs ship, event order, unpaid orders)
- [x] Scope clearly bounded

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - the event, the lock and the tables are in
      research.md, data-model.md and contracts/
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed - Key Entities and Assumptions added in the backfill

## Requirement Completeness

- [x] Success criteria are technology-agnostic - SC-003 names `verify-saga.sh` as its instrument
- [x] All acceptance scenarios are defined
- [x] Dependencies and assumptions identified - Inventory's reservations and Payment's payment row as each
      service's own truth; "staff" meaning Admin

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria - `verify-saga.sh` 47/0 and a full
      refund, per the pull request
- [x] No implementation details leak into the specification

## Notes

**Backfill, 2026-09-27**: the spec gained "Why this priority" and an Independent Test per story,
Given/When/Then scenarios, three edge cases the code handles (an older order in `Preparing` with no parts,
a seller with no part on a cancelled order, a rejected or missing payment), Key Entities and Assumptions.
The code contradicted nothing in the record; the contract gained the as-built status codes and a
`messages.md`.
