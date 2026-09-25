# Specification Quality Checklist: Email confirmation

**Created**: 2026-09-25 | **Feature**: [spec.md](../spec.md)

- [x] No [NEEDS CLARIFICATION] markers - existing accounts, what is gated, lifetime are recorded decisions
- [x] Every rule (single use, concurrency, resend interval, gating, backfill) has acceptance
- [x] Identity from the token for resend; the link's token for confirm
- [x] Scope bounded: no email change, buying not gated
