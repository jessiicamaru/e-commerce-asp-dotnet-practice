# Specification Quality Checklist: Vouchers (part 1 - the server)

> Written on 2026-09-27, after the feature merged (#153), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Purpose**: Validate specification completeness and quality
**Created**: 2026-09-27 (the spec itself: 2026-09-26)
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: column names (`MinSubtotal`,
  `ShopDiscount`) appear in the Rules because the rules are about what is frozen where; the requirements are behaviour
- [x] Focused on user value and business needs - campaigns, first-order rewards, free delivery, and who pays
- [x] Written for non-technical stakeholders - the user's own examples are the acceptance
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain - the design was agreed with the user on 2026-09-26
- [x] Requirements are testable and unambiguous - each has a named test and most a mutation
- [x] Success criteria are measurable - the race, the release, agreement to the unit, 12 of 12 mutations
- [x] Success criteria are technology-agnostic - *explained*: they cite the checks that measured them
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified - races, over-large fixed amounts, free delivery on a free delivery, rounding
- [x] Scope is clearly bounded - screens, categories, editing, public list, shop-funded free delivery are out
- [x] Dependencies and assumptions identified - specs/012, 022, 037, 066, 068

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows - checkout, creation, money downstream
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification - as above

## Notes

This checklist did not exist before the backfill; it was filled in against the spec as merged.
