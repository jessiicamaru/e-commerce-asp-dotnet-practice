# Specification Quality Checklist: Signed in unless an endpoint says otherwise

**Created**: 2026-09-27 | **Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: this feature is about an ASP.NET Core
  mechanism (the fallback policy and two attributes) that the constitution names by name; the stories are in terms
  of who can reach what
- [x] Focused on user value and business needs - nobody reaches an account endpoint by a developer's omission
- [x] Written for non-technical stakeholders, as far as the subject allows
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain - the issue's open choice is recorded as a decision (both)
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic - *explained*: SC-001/002/005 name tests, as every record here does
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (logout, refresh, gRPC with and without a token, orchestrator and gateway, OpenAPI,
  revoked tokens, role endpoints)
- [x] Scope is clearly bounded (service-to-service authentication out)
- [x] Dependencies and assumptions identified (every service calls `AddJwtAuthentication` before mapping)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows (fail closed, stay public, named in a test)
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification - see the first item

## Notes

The choice between a fallback policy and per-controller attributes was made on the user's behalf - both, with a test
- and is recorded in the spec's Decision section and research D1. The survey that sized it is research D3.
