# Specification Quality Checklist: Sign-in refusal

> Completed on 2026-09-27, after the feature merged (#130), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Created**: 2026-09-24 | **Feature**: [spec.md](../spec.md)

- [x] Focused on user value (a locked person learns why and until when)
- [x] No [NEEDS CLARIFICATION] markers
- [x] Requirements testable; acceptance per story
- [x] Edge cases: wrong password on a locked account, unknown 403, no body
- [x] Scope bounded (refresh and #112 out)
- [x] #28 (no account enumeration) preserved explicitly

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: the spec names `code`, `until`,
  `reason` and `ForbiddenException`, because the feature *is* an addition to an API's answer (US2); the user
  story US1 is written without them
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders - US1 is; US2 is for the clients that word the answer
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic - *explained*: SC-003 names the Production environment,
  because the defect it guards against (facts shown only in Development) is environment-specific
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified (specs/043's lock columns, #28, specs/042's "data, not
  sentences")

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria (verified in #130)
- [x] No implementation details leak into specification - see the first item

## Notes

No clarification was asked of the user; the two design choices the pull request records as "decided on the
user's behalf" are [research.md](../research.md) D1 and D3/D4.
