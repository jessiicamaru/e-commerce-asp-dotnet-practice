# Specification Quality Checklist: Finding the images nobody can name

**Created**: 2026-09-23 · **Feature**: [spec.md](../spec.md)

> Completed on 2026-09-27, after the feature merged (#74), from the code at that merge, the pull request and
> docs/features/catalog.md. Items re-read against the completed spec; see the last note.

## Content Quality

- [X] No implementation details in the spec (they are in plan.md and contracts/)
- [X] Focused on the question being asked: how much is being wasted, and can it be reclaimed safely
- [X] Written for non-technical stakeholders
- [X] All mandatory sections completed

## Requirement Completeness

- [X] No [NEEDS CLARIFICATION] markers remain
- [X] Requirements are testable and unambiguous
- [X] Success criteria are measurable
- [X] Success criteria are technology-agnostic
- [X] All acceptance scenarios are defined
- [X] Edge cases identified: a file mid-upload, a failing database read, the store's own bookkeeping
      files, a row changing between the report and the delete
- [X] Scope bounded: no schedule, no object storage, no repairing rows that name missing files
- [X] Dependencies and assumptions identified, including the one that makes this destructive

## Feature Readiness

- [X] All functional requirements have clear acceptance criteria
- [X] User scenarios cover primary flows
- [X] Feature meets measurable outcomes
- [X] No implementation details leak into the specification

## Notes

**This is the first feature in this project whose failure mode is destroying data somebody is
using.** Everything else has failed by refusing, leaking or showing the wrong number. That is why
it never runs on a timer (research D1), why the report stands alone as a shippable outcome
(research D2), and why four dangerous cases are each written red before their guards exist.

**The worst of them is FR-006**: if the catalogue read fails and the code carries on with an empty
live set, every file in the store becomes a candidate. The reconciler reads the live keys first
*because* of that test, not the other way round.

**Backfill, 2026-09-27**: the spec gained the Edge Cases section this checklist already listed, "Why
this priority" and an Independent Test per story, Given/When/Then scenarios and Key Entities. US2's
first scenario was sharpened to match the code: the reclaim removes what **its own** reconciliation
finds, which is the report's set unless something changed in between (FR-005 already said so). The
contract's reclaim example named fields (`removed`, `removedBytes`) the code never had; it is
corrected in place.
