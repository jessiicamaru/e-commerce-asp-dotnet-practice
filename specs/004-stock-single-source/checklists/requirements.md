# Specification Quality Checklist: One Source of Truth for Stock

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-17
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

All items pass. Two clarifications were resolved by the user on 2026-09-17 **before** the spec was
written, because both changed what the feature is rather than how it is built:

- **What a shopper sees.** Chosen: a buyable / not-buyable state, fed from the stock owner. Rejected:
  exposing the real count (same cost to build, but a count three seconds stale reads as a defect and
  publishing exact inventory is a business decision nobody has taken), and removing the figure
  entirely so clients ask the stock service directly (cheapest, but pushes a per-product second call
  onto every listing render).
- **What happens to the stock quantity on product creation.** Chosen: the catalogue stops accepting
  it. Rejected: carrying an opening quantity on the product-created announcement, which is one step
  for an administrator but means the catalogue telling the stock owner what its stock is — the same
  ownership inversion this feature exists to remove.

Both are recorded under Assumptions with their rejected alternative, so a reader a year from now can
see these were decisions rather than defaults.

Two wordings flagged rather than passed quietly against "no implementation details":

- **"delivered twice", "arriving out of order"** (Edge Cases, FR-006, FR-007) name a delivery
  mechanism. Kept, because they are the *cause* of the requirement — a reader who does not know
  announcements can arrive twice or out of order cannot evaluate either requirement.
- **"within 10 seconds"** (FR-010, SC-002, SC-003) is a real user-facing bound, not a technical one:
  it is how long a shopper might stare at a listing that disagrees with reality.

One thing this spec asserts from evidence rather than assumption: the stock owner publishes **no**
message carrying a stock level today. That was read out of the code before FR-004 was written, so
the requirement is known to add something rather than to describe something that exists.

Ready for `/speckit-plan`.
