# Specification Quality Checklist: One rule for "off the shelf"

**Created**: 2026-09-27 | **Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: FR-001-003 name `OnShelf` because the
  requirement is "one definition in one place"; the stories are in terms of what a shopper can open
- [x] Focused on user value and business needs - a product is on sale or it is not, the same everywhere
- [x] Written for non-technical stakeholders, as far as the subject allows
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain - the issue's question (page or 404) is a decision
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic - *explained*: they name tests and a grep, as every record here does
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (saved, cached photograph, views, variants, moderation, rollback)
- [x] Scope is clearly bounded (a withdraw command and a "no longer sold" page out)
- [x] Dependencies and assumptions identified (what `IsActive = false` means; no writer today)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows (every public read; one definition)
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification - see the first item

## Notes

The choice - a withdrawn product is a 404 like a taken-down one - was made on the user's behalf and is recorded in the
spec's Decision section and research D1. The survey that found no writer of `Product.IsActive` is research D2.
