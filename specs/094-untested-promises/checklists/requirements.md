# Specification Quality Checklist: Behaviour promised but not held by a test

**Created**: 2026-09-27 | **Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: a feature whose deliverable is tests names
  the code they hold; each row is stated first as behaviour a person relies on
- [x] Focused on user value and business needs - each row is a promise to somebody (a toast, a bill, a payment, a shop)
- [x] Written for non-technical stakeholders, as far as the subject allows
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain - row 7's fix-or-test choice is a decision
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable (counts of tests, a mutation per row)
- [x] Success criteria are technology-agnostic - *explained*: they name suites, as every record here does
- [x] All acceptance scenarios are defined - one per row
- [x] Edge cases are identified (row 2's two guards, row 3's restart, row 4's variant, row 5's losers)
- [x] Scope is clearly bounded (only #186's rows)
- [x] Dependencies and assumptions identified (mutations reverted to identical source)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows (seven rows; one fix)
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification - see the first item

## Notes

Row 2 needed three attempts at a mutation before one was caught; the reason (two independent guards) is recorded in
research D3 rather than hidden, because it says the behaviour is safer than one test suggests.
