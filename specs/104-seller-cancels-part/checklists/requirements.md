# Specification Quality Checklist: A seller cancels the part of an order they cannot fulfil

**Created**: 2026-09-27 | **Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs). *Explained*: FR-001 to FR-005 name routes, messages
  and columns, because this feature crosses three services, each with a contract. The stories are about sellers and
  buyers.
- [x] Focused on user value and business needs: one seller's problem no longer cancels everybody's goods.
- [x] Written for non-technical stakeholders.
- [x] All mandatory sections completed.

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain. The issue's open questions (delivery share, vouchers, the message) are
  decided in research D2, D5 and D3.
- [x] Requirements are testable and unambiguous.
- [x] Success criteria are measurable.
- [x] Success criteria are technology-agnostic. *Explained*: they name tests, as every record here does.
- [x] All acceptance scenarios are defined.
- [x] Edge cases are identified: a later whole cancel, overtaking the completion, vouchers, delivery, money, rollback.
- [x] Scope is clearly bounded: line-level cancels and email are out.
- [x] Dependencies and assumptions identified: one seller per variant, and deploying the consumers first.

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria.
- [x] User scenarios cover the primary flows: a part, the last part, and the shop's part.
- [x] Feature meets the measurable outcomes defined in Success Criteria.
- [x] No implementation details leak into the specification. See the first item.

## Notes

The riskiest property is money: a buyer must never be refunded a part twice (research D4, SC-003, SC-006).
