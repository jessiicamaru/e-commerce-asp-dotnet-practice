# Specification Quality Checklist: Two Price Lists, Not One Price Converted

> Written on 2026-09-27, after the feature merged (#59), from the code at that merge, the pull request, docs/features/catalog.md and docs/features/shopping-and-checkout.md.

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-27 (retrospectively; the feature merged 2026-09-22)
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) - see Notes
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details) - see Notes
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (section added 2026-09-27)
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria - see Notes on SC-004
- [x] No implementation details leak into specification

## Notes

Written after the fact; the spec was not put through this checklist before planning.

- **Implementation details**: FR-001 names the boundaries an amount crosses (HTTP, gRPC, broker, database)
  and SC-003 names "the existing CHECK constraint". Kept: the requirement *is* that no boundary drops the
  currency, and the constraint is the invariant being protected. Everything else is behavioural.
- **Edge cases**: the spec as written had no edge-case section; it was added on 2026-09-27 from the tests
  and the PR (unsupported code, zero price, fractional dong, older messages, an unrebuilt Orchestrator).
- **SC-004** asked for an automated check of the saga relay. What exists checks the event end automatically
  and the saga end only by reading a payment row on the running stack; the spec's Success Criteria and tasks
  T036 say so rather than tick it silently.
- **Clarifications**: none were raised. The owner asked for "two kinds of price, dollars and dong"; the
  decision that a missing price never falls back (research D3) is the design's, not recorded as the owner's.
