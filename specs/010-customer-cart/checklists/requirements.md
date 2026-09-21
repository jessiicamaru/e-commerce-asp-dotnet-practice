# Specification Quality Checklist: Somewhere to Put What You Intend to Buy

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-21
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

**Two clarifications were asked, not defaulted**, because issue #19 itself marked them as needing a
decision and each changes the size of the work by a large factor: whether the cart is its own
service (answered: yes), and whether guest carts are in scope (answered: deferred). Both are
recorded in the spec's *Decisions already taken* section with the reasoning, rather than hidden in
Assumptions, because a reader should be able to see they were choices.

**Two further questions from #19 were given reasonable defaults instead of being asked**: abandoned
carts do not expire in this feature (no customer-visible criterion exists for it yet), and a
withdrawn product stays in the cart, marked, blocking checkout (the issue itself names silent
dropping as the worst option, which narrows the choice to one sensible answer).

**US3 exists because of feature 009, and is the one most likely to be under-weighted.** A cart is a
new place a price can be held, and therefore a new route for the "customer sets the price" defect
to return — this time through a copy the system made itself. The constitution already forbids it:
*"the non-owning copy MUST NOT inform any decision — most importantly, a charge/refuse decision."*

**US2 acceptance scenario 5 is the subtle one.** "Empty the cart after checkout" and "remove what was
ordered" are different, and only the second survives a customer adding something while checkout is
in flight. Written as a scenario so it cannot be quietly simplified away.
