# Implementation Plan: Audit gaps and the misleading reuse warning

**Branch**: `058-audit-gaps` | **Spec**: [spec.md](spec.md) | **Issue**: #128 (part A)

## Design

- **Catalog:**
  - `SetCategoryTranslationCommandHandler` and `RemoveCategoryTranslationCommandHandler` gain
    `IAuditTrail` and record before their one save.
  - `RemoveProductTranslationCommandHandler` records `ProductTranslationRemoved` before
    `AfterSellerEditAsync`.
- **Identity:**
  - `SetDefaultAddressCommandHandler` records inside its transaction, before the second save.
  - `LogoutCommandHandler` records `SignedOut` with `AuditActors.Of(user)`: sign-out may carry no access
    token, so the actor comes from the row, as it does for sign-in.
  - `RefreshTokenCommandHandler` treats a revoked token as reuse only when `ReplacedByToken` is set and
    the grace window has passed. It records `SessionReuseDetected` before revoking, saved with the
    revocation.

## Constitution check

- III (atomic writes): every entry is staged before the save of the change it describes. Pass.
- V (evidence): each entry has a test that fails first. The stale-tab test fails first on the session it
  ended. Pass.
