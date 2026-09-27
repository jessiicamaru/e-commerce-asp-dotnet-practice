# Specification Quality Checklist: Staff see a person's moderation history

**Created**: 2026-09-27 | **Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs). *Explained*: FR-001 and FR-004 name a message field and a
  route, because they change a contract and open a read to moderators. The stories are about staff deciding.
- [x] Focused on user value and business needs: no decision is made as if it were the first.
- [x] Written for non-technical stakeholders.
- [x] All mandatory sections completed.

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain. The issue's open question (author on the entry, or a lookup) is decided in
  research D1.
- [x] Requirements are testable and unambiguous.
- [x] Success criteria are measurable.
- [x] Success criteria are technology-agnostic. *Explained*: they name tests, as every record here does.
- [x] All acceptance scenarios are defined.
- [x] Edge cases are identified: other categories, entries from before, older publishers, the person's own acts, and
  snapshots.
- [x] Scope is clearly bounded: reports (#199) and staff notes are out.
- [x] Dependencies and assumptions identified: the audit log (specs/041) and reasons already shown to the person
  (specs/059).

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria.
- [x] User scenarios cover the primary flows: deciding with the history in view, reading it from the menu, and content
  decisions counting.
- [x] Feature meets the measurable outcomes defined in Success Criteria.
- [x] No implementation details leak into the specification. See the first item.

## Notes

The feature widens what moderators can read, by one person's Moderation entries at a time. The spec and research D2
state where that line is and why it holds: the category is fixed in code, and snapshots are not returned.
