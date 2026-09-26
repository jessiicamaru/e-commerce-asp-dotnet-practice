# Specification Quality Checklist: Unlock rules

> Completed on 2026-09-27, after the feature merged (#131), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Created**: 2026-09-24 | **Feature**: [spec.md](../spec.md)

- [x] Focused on the harm (a moderator undoing an administrator's lock, a moderator freeing themselves)
- [x] No [NEEDS CLARIFICATION] markers - the one judgement call is recorded as a decision
- [x] Every rule has an acceptance line and a test
- [x] Scope bounded (#112 and bans out)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: FR-001 names
  `ModerationRules.EnsureMayRelease` because the fix is to put the rule where specs/043's rules live; the
  stories are written in terms of who may do what
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (unlocking yourself when not locked, exactly 30 days, a lock run down, a ban)
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified (specs/043's rules and 30-day cap)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria (verified in #131)
- [x] No implementation details leak into specification - see the first item

## Notes

The judgement call - measure reach on the time still to run - was made on the user's behalf and is recorded
in the spec's Decision section and [research.md](../research.md) D2.
