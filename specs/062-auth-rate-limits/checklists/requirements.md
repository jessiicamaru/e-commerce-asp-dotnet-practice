# Specification Quality Checklist: Limits on the sign-in and email endpoints

**Created**: 2026-09-25 | **Feature**: [spec.md](../spec.md)

- [x] #28 preserved: an unknown email and a real one are throttled identically
- [x] No [NEEDS CLARIFICATION] markers - numbers, keys and the "delay, not lock" choice are recorded decisions
- [x] Every rule (per-IP buckets, per-email block, inbox interval, 429 shape, trusted proxies) has acceptance
- [x] Scope bounded: no CAPTCHA, no alert email, no cross-instance gateway counters
