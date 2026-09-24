# Implementation Plan: Review on every seller edit

**Branch**: `056-review-on-every-seller-edit` | **Spec**: [spec.md](spec.md) | **Issue**: #126

## Design

- `SetOptionTranslationCommandHandler` gains `IAuditTrail`. It records `OptionTranslated`, before and
  after, then calls `AfterSellerEditAsync`, then saves once.
- `AddProductVariantCommandHandler` calls `AfterSellerEditAsync` before its save, after its existing
  audit entry.
- The guard lives inside `AfterSellerEditAsync`: approved products only, sellers' products only, never
  for an administrator. No handler repeats it.

## Constitution check

- III (atomic writes): the audit entries, the event and the review change all commit with the one save.
  Pass.
- V (evidence): the two tests fail before the calls exist, and a mutation check removes each call.
  Pass.
