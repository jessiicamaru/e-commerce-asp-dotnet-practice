# Specification Quality Checklist: Shoppers report a review, a question or a product

**Created**: 2026-09-27 | **Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs). *Explained*: the FRs name routes because they are a new
  public surface and a new staff surface. The stories are about shoppers and moderators.
- [x] Focused on user value and business needs: shoppers flag what is wrong, and moderators stop reading everything.
- [x] Written for non-technical stakeholders.
- [x] All mandatory sections completed.

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain. The issue's two open questions, auto-hide and which service owns
  reports, are decided in research D1 and D2.
- [x] Requirements are testable and unambiguous.
- [x] Success criteria are measurable.
- [x] Success criteria are technology-agnostic. *Explained*: they name tests, as every record here does.
- [x] All acceptance scenarios are defined.
- [x] Edge cases are identified: many reports, deletion, acting from another page, rejection, and anonymity of the
  decider.
- [x] Scope is clearly bounded: reporting shops and answers separately, and rate limits, are out of scope.
- [x] Dependencies and assumptions identified: hide and take-down (specs/045, 046, 076) and notices (specs/042, 048).

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria.
- [x] User scenarios cover the primary flows: report, act, dismiss, report again.
- [x] Feature meets the measurable outcomes defined in Success Criteria.
- [x] No implementation details leak into the specification. See the first item.

## Notes

Acting deliberately adds no endpoint. The existing hide and take-down close the reports (research D3), so every page
that hides something agrees with the queue.
