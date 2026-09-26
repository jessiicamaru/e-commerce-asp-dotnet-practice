# Specification Quality Checklist: A seller sees how their shop is doing

> Written on 2026-09-27, after the feature merged (#152), from the code at that merge, the pull request and
> docs/features/seller-insights.md.

**Purpose**: Validate specification completeness and quality
**Created**: 2026-09-27 (the spec itself: 2026-09-25)
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: FR-001/FR-002 name the endpoints, as the
  repository's specs do; the rules (own lines, before tax, sold only, per currency) are stated as behaviour
- [x] Focused on user value and business needs - the four questions a seller asks
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous - each rule has a named test
- [x] Success criteria are measurable - exact sums, the 2.5 example, 403s, suite sizes
- [x] Success criteria are technology-agnostic - *explained*: they cite the checks that measured them
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified - shared orders, several lines per order, returns open and received, no reviews
- [x] Scope is clearly bounded - no comparison, conversion or export; the Overview's returns left for later
- [x] Dependencies and assumptions identified - specs/034, 046, 047, 055, 066

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria - *explained*: by the tests; the live run had no
  sales to show
- [x] No implementation details leak into specification - as above

## Notes

This checklist did not exist before the backfill; it was filled in against the spec as merged.
