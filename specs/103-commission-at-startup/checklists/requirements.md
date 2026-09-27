# Specification Quality Checklist: A missing commission rate stops Order at startup

**Created**: 2026-09-27 | **Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs). *Explained*: FR-001 names the method, because the fix is
  where a check lives. The story is about an operator.
- [x] Focused on user value and business needs: a broken deployment is caught before a customer is.
- [x] Written for non-technical stakeholders.
- [x] All mandatory sections completed.

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain.
- [x] Requirements are testable and unambiguous.
- [x] Success criteria are measurable.
- [x] Success criteria are technology-agnostic. *Explained*: they name tests, as every record here does.
- [x] All acceptance scenarios are defined.
- [x] Edge cases are identified: the other two settings, and tests that replace the rate.
- [x] Scope is clearly bounded: per-seller rates are out.
- [x] Dependencies and assumptions identified: `appsettings.json` keeps shipping `0.1`.

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria.
- [x] User scenarios cover the primary flow.
- [x] Feature meets the measurable outcomes defined in Success Criteria.
- [x] No implementation details leak into the specification. See the first item.

## Notes

A small feature, so short files. None of them is missing (CLAUDE.md, "Every feature gets the full design record").
