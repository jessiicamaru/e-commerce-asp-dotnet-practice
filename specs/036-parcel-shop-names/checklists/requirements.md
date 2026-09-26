# Specification Quality Checklist: Which shop each parcel comes from

**Created**: 2026-09-23 · **Feature**: [spec.md](../spec.md)

> Completed on 2026-09-27, after the feature merged (#80), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. The five items that were here are kept; the rest of the
> Spec Kit checklist is filled in below.

- [X] No implementation details in the spec
- [X] Requirements testable; success criteria measurable
- [X] Edge cases: older orders, a name Catalog has not heard yet, an older Catalog
- [X] Scope bounded: no contact, no profile, no backfill
- [X] No [NEEDS CLARIFICATION] markers

The only decision taken in auto mode: the name is **frozen** at checkout rather than looked up at
read time (research D1) - the same reasoning as the seller id in specs/034.

## Content Quality

- [X] No implementation details (languages, frameworks, APIs)
- [X] Focused on user value and business needs - "which shop is this from", "who do I ask"
- [X] Written for non-technical stakeholders
- [X] All mandatory sections completed - Key Entities and Assumptions added in the backfill

## Requirement Completeness

- [X] No [NEEDS CLARIFICATION] markers remain
- [X] Requirements are testable and unambiguous
- [X] Success criteria are measurable
- [X] Success criteria are technology-agnostic - SC-003 names `verify-saga.sh` as its instrument
- [X] All acceptance scenarios are defined
- [X] Edge cases are identified - plus a blank name on the wire, added from the code
- [X] Scope is clearly bounded
- [X] Dependencies and assumptions identified - Catalog's `sellers` read model and its lag

## Feature Readiness

- [X] All functional requirements have clear acceptance criteria
- [X] User scenarios cover primary flows
- [X] Feature meets measurable outcomes defined in Success Criteria
- [X] No implementation details leak into the specification

## Notes

**Backfill, 2026-09-27**: "Why this priority" and an Independent Test per story, Given/When/Then
scenarios, Key Entities and Assumptions were added. The record had no issue number; the pull request
closes none, and the spec now says so. The code contradicted nothing in the record.
