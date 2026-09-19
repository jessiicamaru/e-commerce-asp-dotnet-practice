# Specification Quality Checklist: Releasable Versions, and Rollbacks That Survive

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-19
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

All items pass. One clarification was resolved by the user on 2026-09-19 before the spec was
written, because the three answers are three different features:

- **Whether the expand/contract rule is enforced.** Chosen: **written down, plus an automatic
  statement on the proposal that does not block**. Rejected: documenting it only — this repository's
  own `microservices-design.md` carries the lesson that *"nothing may read this" is a wish, not a
  constraint*, recorded after a rule of exactly that shape was broken by the endpoint that exposed
  it; and failing the pipeline — a block over a risk that does not yet exist anywhere would be
  overridden routinely, and a guard people learn to override is worse than none. Recorded under
  Assumptions with both rejections.

**Two things this spec deliberately refuses to specify**, because their acceptance could not be
checked today:

- **Deploying, promoting and a rollback command.** There is no server, no cluster, no environment.
  Criteria for them would be unverifiable, which the project's guardrails forbid. The feature stops
  at *a version exists and going back to it is safe*.
- **A retention rule based on usage.** "Keep whatever is still deployed" is the correct rule and is
  unanswerable while nothing is deployed. FR-013 therefore states what is kept and forbids
  age-alone, rather than inventing a policy that cannot be evaluated.

**One wording flagged rather than passed quietly.** FR-003's *"fetching the same identifier returns
the same artifact"* is close to naming a mechanism. It is kept because immutability is the property
that makes the whole feature work — an identifier that can point at two things makes "go back to the
previous version" meaningless — and there is no shorter way to say it.

**Written without naming the technology**, which for a feature about a build pipeline took effort.
"Registry", "image", "tag", "SHA", "GHCR" and "migration" appear nowhere in the requirements. That
forced one useful separation: FR-008 says *removes, renames or narrows part of the database's shape*
rather than naming the operations, so the check has to catch the **shape** of a breaking change and
not a keyword — which is what the edge case about a narrowed field is there to protect.

**One thing asserted from evidence rather than assumption**: the already-released breaking change is
real and specific. `products.StockQuantity` was dropped in PR #5 on 2026-09-17, and every Catalog
build from before that commit selects it on every product query. FR-014 exists because that example
is better than an invented one.

Ready for `/speckit-plan`.
