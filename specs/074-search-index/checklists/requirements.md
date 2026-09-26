# Specification Quality Checklist: Search uses an index

> Written on 2026-09-27, after the feature merged (#158), from the code at that merge, the pull request and docs/features/catalog.md.

**Purpose**: Validate specification completeness and quality
**Created**: 2026-09-27 (the specification itself was written 2026-09-26)
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - **explained**: this is a performance refactor
  whose requirements *are* the mechanism (an IMMUTABLE wrapper, trigram indexes, an escaped `LIKE`), because the
  issue it closes named that mechanism and the observable behaviour is required to stay the same. The user stories
  and success criteria are written in terms of what a shopper gets and what was measured.
- [x] Focused on user value and business needs - a search that stays fast as the catalogue grows, with no change
  in what it finds.
- [x] Written for non-technical stakeholders - the user stories and success criteria are; the requirements are
  deliberately technical, as above.
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous - FR-001 is pinned by the existing search tests, FR-004 by the
  literal-match test, FR-003/FR-006/FR-008 by `SearchIndexTests`, FR-005 by the PR's `EXPLAIN ANALYZE` table.
- [x] Success criteria are measurable - SC-001 to SC-003 are the PR's timings; SC-004 to SC-007 are test counts
  and outcomes.
- [x] Success criteria are technology-agnostic (no implementation details) - **explained**: SC-006 names a test;
  the rest are timings and outcomes.
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified - including the two not measured (short terms, write cost), marked "not recorded".
- [x] Scope is clearly bounded - Out of Scope lists description search, variant SKUs, relevance, full-text search.
- [x] Dependencies and assumptions identified - the `unaccent` extension from specs/021, `pg_trgm` in the image.

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows - a search, a search with wildcard characters, and a regression.
- [x] Feature meets measurable outcomes defined in Success Criteria - per PR #158.
- [x] No implementation details leak into specification - **explained** under Content Quality.

## Notes

- The original spec (2026-09-26) had Why, Requirements and Acceptance only. User stories, edge cases, key
  entities, success criteria, assumptions and out-of-scope were added on 2026-09-27 from the PR and the code.
- FR-006 and FR-007 were found while building (the `OR EXISTS` shape still scanning; Npgsql's `ESCAPE ''`), and
  are recorded as requirements because the merged code enforces them.
- FR-004's SQL was corrected in place against the code; see the note under the requirements.
