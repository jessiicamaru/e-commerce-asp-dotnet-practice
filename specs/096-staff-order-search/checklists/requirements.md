# Specification Quality Checklist: Staff find any order

**Created**: 2026-09-27 | **Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: FR-001 names the route because it is a new
  permission boundary; the stories are in terms of what staff can find
- [x] Focused on user value and business needs - answering "where is my order?"
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain - email search and moderator access are decisions
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic - *explained*: they name tests, as every record here does
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (short prefix, hyphens, both filters, parcels, names)
- [x] Scope is clearly bounded (moderators, export out)
- [x] Dependencies and assumptions identified (Identity's staff search and lookup)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows (id, email, status, permission)
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification - see the first item

## Notes

Two choices were made on the user's behalf - the storefront composes the email search, and the route is Admin-only - in
the spec's Decision section and research D1, D2.
