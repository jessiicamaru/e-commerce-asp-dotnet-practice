# Specification Quality Checklist: A banned seller's shop is closed

**Created**: 2026-09-27 | **Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: FR-001-003 name the message and the column
  because the fix is a new cross-service announcement; the stories are in terms of what a shopper can buy
- [x] Focused on user value and business needs - nobody buys from a shop that can no longer ship
- [x] Written for non-technical stakeholders, as far as the subject allows
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain - the issue's three open questions are decisions (lock, orders in
  flight, eventual consistency)
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic - *explained*: they name tests, as every record here does
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (lock, paid orders, unknown seller, old bans, redelivery, rollback)
- [x] Scope is clearly bounded (closing without a ban, taking parcels over, backfill out)
- [x] Dependencies and assumptions identified (the Seller role decides; sellers keyed by user id)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows (ban, lift, banned applicant, the page)
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification - see the first item

## Notes

Three choices were made on the user's behalf - a ban closes the shop and a lock does not, the suspension is a read model
knowingly, and it is copied onto products - recorded in the spec's Decision section and research D1-D3.
