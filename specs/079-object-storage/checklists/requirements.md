# Specification Quality Checklist: Product images in object storage

> Written on 2026-09-27, after the feature merged (#163), from the code at that merge, the pull request and docs/features/catalog.md.

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-27 (retrospectively; the spec itself is from 2026-09-26)
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - *explained*: this is an infrastructure feature whose
  requirement *is* a technology choice. The user stories and success criteria are written as outcomes (every
  instance serves every image, the orphan report agrees, nothing uploaded is lost); the functional requirements name
  S3, SeaweedFS and `If-None-Match` because the issue asked for an S3-compatible store and the MinIO-to-SeaweedFS
  switch had to be recorded where it was decided
- [x] Focused on user value and business needs - pictures that do not break on a second instance, and a reclaim
  that cannot delete live images
- [x] Written for non-technical stakeholders - the "Why" and the stories are; the requirements are for the people
  who run the stack
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous - each FR maps to a test in `S3ProductImageStoreTests`, the startup
  code, compose or CI
- [x] Success criteria are measurable - SC-001 to SC-007 carry the PR's own numbers (15/15, 0 orphans, 14 copied,
  198/198, seven mutations, 263/263 and 428/428)
- [x] Success criteria are technology-agnostic (no implementation details) - *explained*: SC-005 and SC-007 name the
  test project and Bruno, because "tested against a real S3 server" is itself the criterion (constitution
  Principle V)
- [x] All acceptance scenarios are defined - US1 to US4
- [x] Edge cases are identified - concurrent imports, a leftover probe, unsafe keys, missing keys, a read-only
  volume, an unset or unknown store
- [x] Scope is clearly bounded - no CDN or presigned URLs, the directory store kept, no copy back
- [x] Dependencies and assumptions identified - an S3 server honouring SigV4, path-style and `If-None-Match`; only
  SeaweedFS exercised

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows - serving, reconciling, importing, refusing to start
- [x] Feature meets measurable outcomes defined in Success Criteria - all seven were measured in PR #163
- [x] No implementation details leak into specification - *explained* under Content Quality

## Notes

- The spec as merged had no acceptance scenarios, edge cases, success criteria or assumptions; they were added on
  2026-09-27 from the PR's evidence and the tests.
- FR-003 was corrected on 2026-09-27: `Region` is not a required setting (it defaults to `us-east-1`), and
  `PageSize` is a sixth, range-checked one.
- The one clarification the record shows is the server: the issue named MinIO, MinIO could not be pulled, and
  SeaweedFS was chosen with the user (PR #163). When exactly that was decided is not recorded.
