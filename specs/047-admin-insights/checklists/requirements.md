# Specification Quality Checklist: Admin insights

> Completed on 2026-09-27, after the feature merged (#99, #101), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-24 · **Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - the stories and requirements speak of sales,
  currencies, views and people. FR-001 names the four order statuses, which are the business's own words for
  an order's state; the table and endpoint names live in the plan and the contract
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic - SC-002 names the Bruno collection and SC-005 the test suites and
  `verify-saga.sh`, the project's own checks, as the measure; accepted as the repository's convention (as in
  specs/041)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (currencies missing on old orders, cancellation after payment, renamed and
  deleted products, repeated and anonymous views, empty rankings, period and lookup limits, a moderator at the
  address, what the headline counts include)
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified (the existing review queue and shop applications for the waiting
  counts; UTC days; no view history before the feature)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria - with SC-002 corrected: the collection
  checks each refusal on one endpoint, not on every insight
- [x] No implementation details leak into specification

## Notes

The original checklist (2026-09-24) ticked five items: no implementation details, focused on user value, no
clarification markers, testable requirements and measurable success criteria, bounded scope. The remaining items
were added in the 2026-09-27 completion and checked against the completed spec.

Two corrections made while completing the record, both where the code at the merge disagreed with the first
draft:

- **SC-002** said every insight answers 403 to a moderator and a customer and 401 without a token; the Bruno
  collection asserts each once (customer on `revenue`, moderator on `top-viewed`, no token on `revenue`).
- **The plan's period** said the longest allowed period is 366 days; only the revenue query enforced it
  (FR-011).
