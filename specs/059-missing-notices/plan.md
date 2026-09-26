# Implementation Plan: The notices nobody got

> Completed on 2026-09-27, after the feature merged (#142), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md).

**Branch**: `059-missing-notices` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #128 (part B)

## Summary

Four new notification kinds, declared once and worded in both languages, sent from the three services where the
events happen: Identity (lock, ban), Order (the 7-day delivery sweep) and Catalog (a hidden review). Each notice
goes through the existing `INotifier` and the service's outbox, staged in the transaction of the change it
announces. The storefront formats a lock's end in the reader's language and time zone. No table, endpoint or
message type changed. Decisions are in [research.md](research.md).

## Design

- **`NotificationKind` and `notification-kinds.json`**, four new kinds and their keys:

  | Kind | Required keys |
  | :-- | :-- |
  | `AccountLocked` | `until`, `reason` |
  | `AccountBanned` | `reason` |
  | `ParcelAutoDelivered` | `orderId` |
  | `ReviewHidden` | `product`, `reason` |

- **Identity:** the lock and ban handlers notify before their one save. `until` is ISO 8601 UTC, and the
  storefront words it.
- **Order:** `AutoConfirmDeliveriesCommandHandler`'s stage reads each delivered parcel's order facts, and
  `OrderNotices.AutoDeliveredAsync` tells that parcel's seller. It is inside the sweep's transaction, like
  its audit entry and `ParcelDeliveredEvent`.
- **Catalog:** the hide stage (specs/057) also notifies the author.
- **Storefront:**
  - `describeNotification(t, n, language)` formats `until` with `toLocaleString(language)`;
  - both callers pass `i18n.language`;
  - wording goes in `locales/{en,vi}/notifications.json`.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript with React 19 in the storefront

**Primary Dependencies**: `Ecommerce.Shared.Notifications` (`INotifier`, `NotificationKind`,
`notification-kinds.json`), MassTransit 8.3.6 EF Core outbox, react-i18next

**Storage**: No schema change. Notices are staged in each publisher's outbox and kept by Activity in
`notifications` (`ecommerce_activity_db`, 5440)

**Testing**: xUnit against real PostgreSQL with MassTransit's test harness (Identity `ModerationTests`, Order
`NotificationTests`, Catalog `ReviewTests`); Vitest for `client/src/utils/notifications/index.test.ts`

**Target Platform**: Identity (5056), Catalog (5057), Order (5059), the storefront

**Project Type**: Fix across three services and the storefront

**Performance Goals**: None; one outbox row per notice. The sweep reads each delivered parcel's order facts once
per parcel

**Constraints**: Staged in the deciding transaction (Principle III); a notice stores a kind and data, never a
sentence (specs/042); every key declared (specs/048)

**Scale/Scope**: Four kinds, four handlers, one storefront function and its two callers

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md).

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service tells people about its own events through its own outbox; Activity stores notices it is sent and owns nothing it did not receive. The kinds file is in `Ecommerce.Shared`, the place for cross-cutting declarations |
| **II. Clean Architecture Layering** | **Pass.** Handlers depend on `INotifier` (an abstraction in Shared); the Order repository methods used by the sweep's stage are declared in Application and implemented in Infrastructure |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Lock and ban notify before their one save; the sweep and the hide notify inside the `stage` callback of their guarded statements, so the notice commits with the change or not at all. Activity stores each notice once, keyed by `NotificationId` |
| **IV. Identity Comes From the Token** | **Pass.** Recipients come from the rows (the locked user, the review's `CustomerId`, the parcel's `SellerId`); the acting staff member comes from `ICurrentUser` as before. No request names a recipient |
| **V. Evidence Over Assumption** | **Pass.** Each notice has a server test that failed first and checks the notice against the declaration; the storefront contract test failed first (9 red). Three mutations were run and each turned a test red (PR #142) |

**Post-design re-check**: no violations.

## Constitution check

- III (atomic writes): every notice is staged in the transaction of the change it announces. Pass.
- V (evidence): each notice has a server test that fails first. The storefront contract test fails first
  on the four kinds it has no words for. Pass.

(The two lines above are the check as first written; the table extends it to all five principles.)

## Project Structure

### Documentation (this feature)

```text
specs/059-missing-notices/
├── spec.md
├── plan.md                  # This file
├── research.md              # Five decisions
├── data-model.md            # No schema change; the four kinds and their data
├── quickstart.md
├── contracts/
│   └── messages.md          # UserNotificationRequested: the four kinds, publishers, keys
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (touched by #142)

```text
server/src/BuildingBlocks/Ecommerce.Shared/Notifications/
├── Notifier.cs                          # four NotificationKind constants
└── notification-kinds.json              # four declarations
server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs        # lock, ban
server/src/Services/Order/Ecommerce.Order.Application/Orders/Commands/ConfirmDelivery/DeliveryCommands.cs
server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/OrderNotices.cs             # AutoDeliveredAsync
server/src/Services/Catalog/Ecommerce.Catalog.Application/Reviews/ReviewFeatures.cs             # hide
server/tests/Ecommerce.Identity.Tests/ModerationTests.cs
server/tests/Ecommerce.Order.Tests/NotificationTests.cs
server/tests/Ecommerce.Catalog.Tests/ReviewTests.cs
client/src/utils/notifications/index.ts                  # describeNotification(t, n, language)
client/src/utils/notifications/index.test.ts
client/src/components/layout/notification-bell/index.tsx # passes i18n.language
client/src/pages/notifications/index.tsx                 # passes i18n.language
client/src/locales/{en,vi}/notifications.json
```

Documentation touched in the same change: `CLAUDE.md`, `docs/features/audit-and-notifications.md`,
`docs/overview/project-overview.md`, `docs/project/backlog.md`, `docs/project/timeline.md`,
`docs/testing/testing-strategy.md`.

**Structure Decision**: No new file. Each notice sits in the handler or stage that makes the change, beside its
audit entry.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- Nothing tells a person their lock was lifted or their ban removed.
- None of these events sends an email; email as a channel starts with [specs/060](../060-email/).
- The wording is fixed in the bundle; administrators could reword notices only from specs/078, which also added
  `placeholders` to `notification-kinds.json`.
