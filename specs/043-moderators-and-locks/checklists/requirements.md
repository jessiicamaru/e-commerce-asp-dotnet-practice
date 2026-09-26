# Specification Quality Checklist: Moderators, locks and bans

> Completed on 2026-09-27, after the feature merged (#95), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-24 · **Feature**: [spec.md](../spec.md)

The original checklist, kept as written:

- [x] No implementation details in the spec
- [x] Focused on user value
- [x] No [NEEDS CLARIFICATION] markers remain (lock limits decided with the user)
- [x] Requirements testable; success criteria measurable
- [x] Scope bounded

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - with one exception kept deliberately: the
  status codes (401, 403, 409) are part of what the person and the storefront see, and FR-006 names
  "Staff" as a role list because the next three features depend on that exact thing
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders - the stories read as what an administrator, a moderator and a
  stopped person experience
- [x] All mandatory sections completed - Scenarios & Testing, Requirements, Success Criteria; the
  "Independent Test" and "Why this priority" lines and Given/When/Then forms were added on 2026-09-27

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic - except SC-001, which names Bruno as the evidence; kept as
  written and corrected in place to say which half Bruno actually proves
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified - added on 2026-09-27 from the code: a lock that has run out, overlapping
  lock and ban, repeats as no-ops, re-locking, unknown ids
- [x] Scope is clearly bounded - Out of scope names the access-token window, account deletion, unlock
  rules, telling a stopped person, and closing a shop
- [x] Dependencies and assumptions identified - the audit log (specs/041), notifications (specs/042), the
  15-minute access token, refresh re-reading roles

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows - grant, revoke, lock, unlock, ban, lift, the record, the console
- [x] Feature meets measurable outcomes defined in Success Criteria - evidence in #95: Identity tests
  65/65, client 195/195, Bruno 148/148 requests and 238 tests, four mutation checks each red
- [x] No implementation details leak into specification

## Notes

Decided with the user on 2026-09-24 (issue #88): a moderator may lock a customer or seller for up to 30
days; only an administrator bans, lifts a ban, or grants and revokes roles. That settled the one open
question the issue raised.

One gap in the rules was not visible from the spec at the time: it said who may **stop** whom but nothing
about who may **unlock** whom. It was found afterwards as #121 and closed by specs/050.
