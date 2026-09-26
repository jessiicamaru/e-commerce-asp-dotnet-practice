# Specification Quality Checklist: Limits on the sign-in and email endpoints

> Completed on 2026-09-27, after the feature merged (#145), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../../docs/features/auth/security-best-practices.md).

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-25 | **Feature**: [spec.md](../spec.md)

- [x] #28 preserved: an unknown email and a real one are throttled identically
- [x] No [NEEDS CLARIFICATION] markers - numbers, keys and the "delay, not lock" choice are recorded decisions
- [x] Every rule (per-IP buckets, per-email block, inbox interval, 429 shape, trusted proxies) has acceptance
- [x] Scope bounded: no CAPTCHA, no alert email, no cross-instance gateway counters

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: FR-001 and FR-002 name the ASP.NET
  Core rate limiter, YARP and `TooManyRequestsException`. They were written as design constraints and are kept;
  the user-facing requirements (FR-005 to FR-009) state numbers and behaviour only
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders - *explained*: the stories are; the proxy rules are for reviewers
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification - *explained* as under Content Quality

## Notes

The first four items are the checklist as written on 2026-09-25 and are kept. The Spec Kit sections were added on
2026-09-27. Two details of the plan were corrected against the code: the reset interval is a constant
(`ResetTokens.MinimumInterval`), not a `PasswordReset:MinimumIntervalSeconds` setting, and the network list is
`KnownIPNetworks`.
