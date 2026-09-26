# Specification Quality Checklist: Locking again obeys the unlock rule

**Created**: 2026-09-27 | **Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: FR-002 names `ModerationRules`
  because the fix is to put the rule beside the two it completes; the stories are in terms of who may do what
- [x] Focused on user value and business needs - an administrator's decision stays until an administrator changes it
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain - the issue's open choice (refuse or keep the longer lock) is
  recorded as a decision
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic - *explained*: SC-001/002 name tests, as every record here does
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (same end date, exactly 30 days, self and moderators, bans, sessions, concurrency)
- [x] Scope is clearly bounded (concurrency, `LockedBy`, bans out)
- [x] Dependencies and assumptions identified (specs/043's cap, specs/050's reach rule)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows (refused, administrator, extend, within reach, run out)
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification - see the first item

## Notes

Both judgement calls - refuse rather than keep the longer lock, and let a moderator shorten within reach - were
made on the user's behalf and are recorded in the spec's Decision section and research D1/D2.
