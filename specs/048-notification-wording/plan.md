# Implementation Plan: Notification wording

**Branch**: `048-notification-wording` | **Spec**: [spec.md](spec.md) | **Issue**: #119

## Technical context

- Client: React 19 + i18next. The fix is in `client/src/utils/notifications/index.ts`, with tests beside
  it in Vitest.
- Server: `Ecommerce.Shared/Notifications` (`NotificationKind`, `INotifier`). Notices are sent from:
  - Order: `OrderNotices`;
  - Catalog: `ProductReviewFeatures`, `ReviewFeatures`;
  - Identity: `ShopApplicationFeatures`, `UserAdministration`.
- Existing tests capture every published `UserNotificationRequested` through the MassTransit test
  harness, with the real `Notifier`:
  - `Order.Tests/NotificationTests`;
  - `Catalog.Tests/ProductReviewTests`, `ReviewTests`;
  - `Identity.Tests/ShopApplicationTests`, `ModerationTests`.

## Decisions

**D1 - One declaration, in JSON, beside `NotificationKind`.** It lives at
`Ecommerce.Shared/Notifications/notification-kinds.json`, maps each kind to its `required` and `optional`
data keys, and is embedded in `Ecommerce.Shared`. The server tests read it through
`NotificationContract`. The client test imports the same file by relative path.

Rejected alternatives:
- *A list on each side*: this is what drifted.
- *Code generation*: a build step for sixteen entries.
- *Parsing the C# from the client test*: fragile.

**D2 - Checked by the tests, never at run time.** `Notifier` could validate the data and throw. But a
wording mismatch would then roll back a payout or a moderation decision, which is a far worse defect than
a clumsy sentence. The existing tests already publish every kind through the real `Notifier`. Asserting on
what they capture checks the call sites that actually run, not a copy of them.

**D3 - A hole means the generic sentence.** `describeNotification` reads the sentence's raw form
(`skipInterpolation`) and lists its placeholders. If any would be empty, it uses `generic`. This is
generic over kinds, so a future kind cannot reintroduce the defect silently.

**D4 - Plural rating.** English `NewReview_one` / `NewReview_other` with `count` = the rating. Vietnamese
has no plural and keeps the one string.

## Constitution check

- I (autonomy): no new cross-service call. The declaration belongs to `Ecommerce.Shared`, which already
  owns `NotificationKind`. Pass.
- III (atomic writes): unchanged. D2 keeps notices from affecting whether a write commits. Pass.
- IV (identity from the token): untouched. Pass.
- V (evidence): the defect is reproduced by a failing client test before the fix. The declaration is
  checked against what the running code publishes, not against a hand-written copy. Pass.

## Files

- `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json` (new, embedded)
- `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/NotificationContract.cs` (new)
- `server/src/BuildingBlocks/Ecommerce.Shared/Ecommerce.Shared.csproj` (embed)
- The five server test files above, plus one kinds-match test
- `client/src/utils/notifications/index.ts`, `index.test.ts`
- `client/src/locales/en/notifications.json` (plural)
- `docs/features/audit-and-notifications.md`, `docs/project/*`
