# Specification Quality Checklist: The picture follows the variant

**Created**: 2026-09-23 · **Feature**: [spec.md](../spec.md)

## Content Quality

- [X] No implementation details in the spec (they are in plan.md and contracts/)
- [X] Focused on what a shopper sees
- [X] Written for non-technical stakeholders
- [X] All mandatory sections completed

## Requirement Completeness

- [X] No [NEEDS CLARIFICATION] markers remain
- [X] Requirements are testable and unambiguous
- [X] Success criteria are measurable
- [X] Success criteria are technology-agnostic
- [X] All acceptance scenarios are defined
- [X] Edge cases identified: no image anywhere, the reused first-variant id, deletion
- [X] Scope bounded: no gallery, no image on the order line, the card unchanged
- [X] Dependencies and assumptions identified

## Feature Readiness

- [X] All functional requirements have clear acceptance criteria
- [X] User scenarios cover primary flows
- [X] Feature meets measurable outcomes
- [X] No implementation details leak into the specification

## Notes

**The design was reversed by the data.** Per-option-value was proposed first and is recorded as
rejected in research D1: Fujifilm X-T5 has two black variants that do not look alike because one has
a lens on it, and no rule could tell the system which option axis is the visual one.

**A second finding was measured, not assumed**: 12 of 12 products have a variant whose id equals the
product id, so variant keys need a discriminator or two rows can name the same file.
