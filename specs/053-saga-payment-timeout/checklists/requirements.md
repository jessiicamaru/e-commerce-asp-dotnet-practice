# Specification Quality Checklist: Saga payment timeout

**Created**: 2026-09-24 | **Feature**: [spec.md](../spec.md)

- [x] Focused on the harm (paid for stock no longer held; an order settling forever)
- [x] No [NEEDS CLARIFICATION] markers - the mechanism choice is argued in the plan
- [x] Acceptance per story, including the race with a payment just before the timeout
- [x] Rollback behaviour stated
- [x] Scope bounded (reservation timeout, customer-visible refund out)
