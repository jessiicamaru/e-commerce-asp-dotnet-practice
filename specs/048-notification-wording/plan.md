# Implementation Plan: Notification wording

> Completed on 2026-09-27, after the feature merged (#129), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Branch**: `048-notification-wording` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #119

## Summary

Fix five notification kinds that showed raw `{{placeholders}}`, and stop the class of defect recurring.
Each kind's data keys are declared once, in `Ecommerce.Shared/Notifications/notification-kinds.json`. The
server's existing notification tests check every notice they publish through the real `Notifier` against
it; the storefront's tests check every declared kind reads as a sentence in both languages. No table,
endpoint or message changed. The decisions are in [research.md](research.md) and summarised below as they
were first written.

## Technical context

**Language/Version**: C# / .NET 10.0 (server tests and one shared class); TypeScript, React 19, i18next
(storefront)

**Primary Dependencies**: `System.Text.Json` for the declaration; MassTransit's test harness (already used
by the notification tests); Vitest

**Storage**: none changed

**Testing**: the existing server notification tests against a real PostgreSQL, extended with one
assertion each and one new test; a rewritten client test file

**Target Platform**: the storefront bundle and the server test suites

**Project Type**: a client fix plus a shared declaration and test-only checks

**Constraints**: the declaration must never be enforced at run time (spec FR-004)

**Scale/Scope**: 16 kinds, 10 distinct data keys

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

Full reasoning, and D5 (the "the shop" default), in [research.md](research.md).

## Constitution check

Against [constitution.md](../../.specify/memory/constitution.md).

- I (autonomy): no new cross-service call. The declaration belongs to `Ecommerce.Shared`, which already
  owns `NotificationKind`. Pass.
- III (atomic writes): unchanged. D2 keeps notices from affecting whether a write commits. Pass.
- IV (identity from the token): untouched. Pass.
- V (evidence): the defect is reproduced by a failing client test before the fix. The declaration is
  checked against what the running code publishes, not against a hand-written copy. Pass.

| Principle | Verdict |
| :-- | :-- |
| **I. Service Autonomy** | **Pass.** No new cross-service call, no new message. The declaration lives in `Ecommerce.Shared` beside `NotificationKind`, which it describes; the storefront reads it at test time only. |
| **II. Clean Architecture Layering** | **Pass.** No service layer changed. `NotificationContract` is a static reader in `Ecommerce.Shared` used only by tests; nothing in Domain or Application references it. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Unchanged. D2 keeps notices from affecting whether a write commits: a run-time check that threw inside `Notifier` would roll back the change it announces. |
| **IV. Identity Comes From the Token** | **Pass.** Untouched: no endpoint, no command. |
| **V. Evidence Over Assumption** | **Pass.** The defect was reproduced by 16 failing client tests before the fix. The server side is checked against notices published by the real `Notifier` through the real handlers against PostgreSQL, not a hand-written copy; five mutations each turned a test red. |

**Post-design re-check**: no violation. The one judgement is that the contract is enforced by tests and
not at run time (D2); that is a choice between two defects, recorded, not a principle waived.

## Files

- `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json` (new, embedded)
- `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/NotificationContract.cs` (new)
- `server/src/BuildingBlocks/Ecommerce.Shared/Ecommerce.Shared.csproj` (embed)
- The five server test files above, plus one kinds-match test
- `client/src/utils/notifications/index.ts`, `index.test.ts`
- `client/src/locales/en/notifications.json` (plural)
- `docs/features/audit-and-notifications.md`, `docs/project/*`

## Project Structure

### Documentation (this feature)

```text
specs/048-notification-wording/
├── spec.md
├── plan.md              # this file
├── research.md          # D1-D5
├── data-model.md        # no table changed; the declaration's shape
├── quickstart.md
├── contracts/
│   └── messages.md      # the data each kind of UserNotificationRequested carries
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

```text
server/src/BuildingBlocks/Ecommerce.Shared/
├── Ecommerce.Shared.csproj                          # EmbeddedResource, LogicalName
│                                                    #   Ecommerce.Shared.Notifications.notification-kinds.json
└── Notifications/
    ├── notification-kinds.json                      # new
    └── NotificationContract.cs                      # new: Kinds, KindsInCode, Problems

server/tests/
├── Ecommerce.Order.Tests/NotificationTests.cs       # Sent() asserts no Problems; new kinds-match test
├── Ecommerce.Catalog.Tests/ProductReviewTests.cs    # Notices() asserts; ProductTakenDown now asserted
├── Ecommerce.Catalog.Tests/ReviewTests.cs
├── Ecommerce.Identity.Tests/ShopApplicationTests.cs # ShopRejected now asserted
└── Ecommerce.Identity.Tests/ModerationTests.cs      # ModeratorRevoked now asserted

client/src/
├── utils/notifications/index.ts                     # describeNotification
├── utils/notifications/index.test.ts
└── locales/en/notifications.json                    # NewReview_one / NewReview_other
```

Also changed: `CLAUDE.md` (the notification paragraph), `docs/features/audit-and-notifications.md`,
`docs/project/backlog.md`, `docs/project/timeline.md`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected |
| :-- | :-- | :-- |

## What this feature does not finish

- The declaration is checked only for the notices the existing tests publish. A new `NotifyAsync` call
  site that no test exercises is not checked; the kinds-match test catches a new kind, not a new call
  site.
- The notices #128 lists as missing are not added.
- Product names in notices stay in the seller's language.
- An administrator rewording notices came later (specs/078), which also added `placeholders` to the
  declaration.
