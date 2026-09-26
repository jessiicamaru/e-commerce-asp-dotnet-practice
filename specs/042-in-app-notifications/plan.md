# Implementation Plan: In-app notifications

> Completed on 2026-09-27, after the feature merged (#94), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Branch**: `042-in-app-notifications` | **Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

## Summary

Give every person an inbox. A service that changes something somebody should hear about calls
`INotifier.NotifyAsync(recipient, kind, data, link)`, which publishes `UserNotificationRequested` through that
service's transactional outbox in the same transaction as the change. The Activity service (specs/041) stores
each message once, keyed on the id the publisher minted, in a new `notifications` table, and serves the
caller's own inbox at `/api/notifications`. The storefront puts a bell in the top bar that polls the unread
count every 30 seconds and a `/notifications` page, and words each notice from its kind and data in the
reader's language.

At this merge Order is the only publisher: the buyer hears of paid, failed, shipped and cancelled orders; each
seller of a new sale, a cancelled sale, a received parcel and a payout. Later features add their kinds through
the same interface.

One thing the design did not anticipate surfaced only in the running stack and is folded in: **settling an
order inside the saga's consumer must join the transaction MassTransit already holds** (research D6).

## Technical Context

- **Contract** `Activity/UserNotificationRequested(NotificationId, RecipientId, Kind, Data, Link, OccurredAt)`.
- **Shared** `Notifications/INotifier` (+ `NotificationKind`), published through the caller's outbox like
  `IAuditTrail` - before the one save, or inside a repository `stage`.
- **Activity**: `notifications` (id PK, recipient, kind, data `jsonb`, link, created, read at; index on
  recipient + created); consumer inserts `ON CONFLICT DO NOTHING`; `GET /api/notifications`
  (`unreadOnly`, paging), `GET /api/notifications/unread-count`, `POST /api/notifications/{id}/read`,
  `POST /api/notifications/read-all` - all for the caller only.
  *Completed on 2026-09-27:* the table also has a partial index `IX_notifications_unread` on `RecipientId`
  where `ReadAt IS NULL`, for the count the bell polls ([data-model.md](data-model.md)).
- **Order**: `TrySettleAsync` takes a `stage`; `GetNoticeFactsAsync` (buyer, total, currency, parts with
  seller); `OrderNotices` sends: paid, failed, shipped, cancelled (customer); new sale, sale cancelled,
  received, payout (sellers). Audit `OrderPaid` joins the settle too.
  *Completed on 2026-09-27:* so does `OrderFailed`, on the failure settle.
- **Gateway**: `/api/notifications/**`.
- **Client**: `services/notifications`, `hooks/notifications` (30 s polling), `components/layout/notification-bell`,
  `pages/notifications`, `utils/notifications` (kind → words + link), `notifications` locale namespace.

**Language/Version**: C# 13 / .NET 10; TypeScript with React 19 in the storefront

**Primary Dependencies**: MassTransit 8.3.6 (RabbitMQ, EF Core outbox and inbox), MediatR 12.4.1,
FluentValidation 12.1.1, Npgsql EF Core 10.0.3, `Ecommerce.Shared`, `Ecommerce.Contracts`; storefront:
TanStack Query, react-i18next, shadcn/ui (`dropdown-menu`), lucide-react

**Storage**: PostgreSQL 16, `ecommerce_activity_db` on host port 5440 - one new table. No change to Order's
schema

**Testing**: xUnit against a real PostgreSQL (Activity 5440, Order 5434); Order's through MassTransit's test
harness, reading what was published; Vitest + Testing Library in `client/`; Bruno; `verify-saga.sh`

**Target Platform**: The existing services - Activity on 5063, Order on 5059 - behind the YARP gateway on 5000

**Project Type**: Additions to two existing Clean Architecture services, a Shared building block, a contract,
a gateway route and the storefront

**Performance Goals**: The bell's poll is one indexed count per signed-in visible tab every 30 seconds. No
throughput target was recorded

**Constraints**: A notice commits with its change or not at all (Principle III); one row per notice id; a
reader sees only their own inbox (Principle IV); stored as kind + data, never a sentence

**Scale/Scope**: Eight kinds from one publisher. Nothing is deleted; the table grows without bound (a known
limit)

## Research

- **D1 - Kind + data, not text.** A sentence stored in the language of the moment would stay in it; the
  storefront words it from `Kind` and `Data` in whatever language the reader has now.
- **D2 - Same transaction as the change.** Settling, moving a parcel, confirming a delivery, cancelling
  and paying out are guarded statements in their own transactions; the notification is staged there.
- **D3 - Polling.** 30 s on the unread count only; the list loads when the bell opens.

*Completed on 2026-09-27:* these three, with their alternatives, and six more reconstructed from the code -
D4 the inbox in Activity, D5 idempotent by the publisher's id, D6 settling joins the consumer's transaction,
D7 the reader in the `WHERE` and not-yours as 404, D8 who is told what in one place, D9 an unknown kind shown
generically - are in [research.md](research.md).

## Constitution Check

*Completed on 2026-09-27.* The table as first recorded:

| Principle | Verdict |
| :-- | :-- |
| I | Activity owns the inboxes; Order only publishes. |
| III | Staged with the change; idempotent by id. |
| IV | Recipient from the event; reader from the token; nobody reads another's. |
| V | Tests for each event, idempotence, ownership, marking; client tests for the bell and wording. |

Evaluated in full against [constitution.md](../../.specify/memory/constitution.md) v1.1.0:

| Principle | Assessment |
| :-- | :-- |
| **I. Service Autonomy** | **Pass.** Activity owns the inboxes; Order only publishes. Order never reads or writes `ecommerce_activity_db`, and Activity never asks Order for anything: every fact a notice needs travels in its `Data`. The one new shared type is a pure record in `Ecommerce.Contracts`; `INotifier` is cross-cutting infrastructure in `Ecommerce.Shared`, like `IAuditTrail` |
| **II. Clean Architecture Layering** | **Pass on the dependency rule; one deviation from the folder rule, recorded in Complexity Tracking.** `Notification` is in Activity's Domain; `INotificationRepository` is declared in Application's `Common/Interfaces/` and implemented in Infrastructure; the consumer and controller are in WebApi and only dispatch through MediatR. In Order, `OrderNotices` is Application code that talks to `INotifier` (an abstraction over `IPublishEndpoint`, from `MassTransit.Abstractions`). The deviation: Activity's five use cases share one file, `Notifications/NotificationFeatures.cs`, rather than `Notifications/Commands/<UseCase>/` and `Queries/<UseCase>/` folders - unlike the audit log beside it, which specs/041 split into `Audit/Commands/` and `Audit/Queries/` |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Staged with the change; idempotent by id. Every `NotifyAsync` is before the one save or inside a repository `stage` that runs only when the guard won, so a repeat or a lost race notifies nobody. Activity's insert is guarded by the primary key (`ON CONFLICT DO NOTHING`), not by the inbox alone. The consumer-transaction bug (research D6) was a Principle III failure in the making - a second transaction beside the consumer's - and the fix keeps settle, audit, notices and inbox row in one commit |
| **IV. Identity Comes From the Token** | **Pass.** Recipient from the event; reader from the token; nobody reads another's. No endpoint and no command carries a user id; every statement has the caller's id from `ICurrentUser` in its `WHERE`; someone else's id is the same 404 as a missing one. The controller is `[Authorize]`, no anonymous endpoint |
| **V. Evidence Over Assumption** | **Pass.** Tests for each event, idempotence, ownership, marking; client tests for the bell and wording. They run against a real PostgreSQL because the guarantees are the database's (the primary key under four concurrent deliveries, the guarded updates). The unit tests were **not** enough: they passed while every order stayed `Submitted` in the running stack, and the fix came with a test that opens the transaction the way MassTransit does, shown red with the fix disabled. Three mutation checks are recorded in the pull request |

**Post-design re-check**: no violation of a principle's substance. The folder deviation under Principle II is
listed in Complexity Tracking below, as the constitution's Compliance section requires; it was not recorded
at the time and is written down here. The one addition the design did not foresee - joining the consumer's
transaction - strengthens Principle III rather than bending it.

## Project Structure

### Documentation (this feature)

```text
specs/042-in-app-notifications/
├── spec.md              # User stories, requirements, success criteria
├── plan.md              # This file
├── research.md          # D1-D9 with rejected alternatives
├── data-model.md        # The notifications table, its indexes and guards
├── quickstart.md        # Validation scenarios and what was run
├── contracts/
│   ├── http-api.md      # /api/notifications
│   └── messages.md      # UserNotificationRequested and the eight kinds
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task list, all done
```

### Source Code (repository root)

```text
server/src/BuildingBlocks/
├── Ecommerce.Contracts/Activity/UserNotificationRequested.cs        # new
└── Ecommerce.Shared/Notifications/Notifier.cs                       # new: INotifier, Notifier, NotificationKind, AddNotifier

server/src/Services/Activity/
├── Ecommerce.Activity.Domain/Entities/Notification.cs               # new
├── Ecommerce.Activity.Application/
│   ├── Common/Interfaces/INotificationRepository.cs                 # new
│   └── Notifications/NotificationFeatures.cs                        # new: record, list, count, mark one, mark all
├── Ecommerce.Activity.Infrastructure/
│   ├── Persistence/ActivityDbContext.cs                             # + Notifications
│   ├── Persistence/Configurations/NotificationConfiguration.cs      # new
│   ├── Persistence/Repositories/NotificationRepository.cs           # new
│   ├── DependencyInjection.cs                                       # + repository
│   └── Migrations/20260923195702_AddNotifications.cs                # new
└── Ecommerce.Activity.WebApi/
    ├── Consumers/RecordNotificationConsumer.cs                      # new
    ├── Controllers/NotificationsController.cs                       # new
    └── Program.cs                                                   # + consumer

server/src/Services/Order/
├── Ecommerce.Order.Application/
│   ├── Common/Interfaces/IOrderRepository.cs                        # TrySettleAsync(stage), GetNoticeFactsAsync
│   ├── Orders/Common/OrderNotices.cs                                # new: OrderNoticeFacts, who is told what
│   ├── Orders/Common/ParcelAudit.cs                                 # + RecordMoveAsync (audit + shipped notice)
│   └── Orders/Commands/
│       ├── CompleteOrder/CompleteOrderCommandHandler.cs             # paid + new sale + OrderPaid audit
│       ├── FailOrder/FailOrderCommandHandler.cs                     # failed + OrderFailed audit
│       ├── Fulfilment/FulfilmentStep.cs, ShipOrderCommandHandler.cs # staff ship → shipped
│       ├── SellerFulfilment/SellerFulfilmentCommands.cs             # seller ship → shipped
│       ├── ConfirmDelivery/DeliveryCommands.cs                      # received
│       ├── CancelOrder/CancelOrderCommands.cs                       # cancelled + sale cancelled
│       └── RecordPayout/RecordPayoutCommand.cs                      # payout
├── Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderRepository.cs   # join-or-open settle; facts
└── Ecommerce.Order.WebApi/Program.cs                                # + AddNotifier()

server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json          # notifications-route, notifications-root-route

server/tests/
├── Ecommerce.Activity.Tests/NotificationTests.cs                    # new, 4 tests
├── Ecommerce.Activity.Tests/ActivityTestFixture.cs                  # + CurrentUser, repository
├── Ecommerce.Order.Tests/NotificationTests.cs                       # new, 6 tests
└── Ecommerce.Order.Tests/OrderTestFixture.cs                        # + AddNotifier()

client/src/
├── services/notifications/{index.ts,types.ts,index.test.ts}
├── hooks/notifications/index.ts
├── constants/notifications/index.ts                                 # NOTIFICATION_POLL_MS = 30_000
├── constants/query-keys/index.ts                                    # + unreadCount, notifications
├── utils/notifications/{index.ts,index.test.ts}                     # describeNotification
├── components/layout/notification-bell/{index.tsx,index.test.tsx}
├── components/layout/top-bar/index.tsx                              # + the bell, signed in only
├── pages/notifications/{index.tsx,index.test.tsx}
├── routes/index.tsx                                                 # /notifications behind RequireAuth
├── config/i18n/index.ts                                             # + notifications namespace
└── locales/{en,vi}/{notifications.json,admin.json}                  # words; audit labels OrderPaid, OrderFailed

bruno/notifications/                                                 # six requests
bruno/security-checks/notifications without a token is 401.yml
```

**Structure Decision**: No new project (see Complexity Tracking for the one file that departs from the
use-case folders). The inbox joins the Activity service beside the audit log (research
D4), following the shape specs/041 gave it; the publisher side mirrors `IAuditTrail` exactly, so a service
that already records audit entries learns nothing new to send notices. The storefront follows the client
README's conventions: a service class, a hooks module, a component folder, a page folder.

## Complexity Tracking

> One entry, reconstructed on 2026-09-27. No justification was recorded at the time; the reasoning below is
> read from the code and says so.

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| Principle II's "features are foldered by use case": Activity's five notification use cases (record, list, count, mark one, mark all) live in one file, `Notifications/NotificationFeatures.cs` | Not recorded. Each use case is a record and a handler of a few lines over one repository, and the five share a response type and one `Caller.Of` helper | The folder-per-use-case layout was the conventional alternative and costs nothing but files; why it was not used here was not recorded. The dependency direction - the substance of Principle II - is intact |

## What this feature does not finish

- **Only Order speaks.** Shop and product decisions, moderator roles and reviews notify only once their own
  features land (#89-#91 and after); Identity and Catalog register `AddNotifier()` then.
- **A parcel the 7-day sweep takes as delivered tells its seller nothing** at this merge; the customer's
  confirmation does. `ParcelAutoDelivered` came with specs/059.
- **The data keys are agreed by convention.** Nothing checks that a kind's `Data` carries what its sentence
  reads; specs/048 (#119) declared the keys in `notification-kinds.json` and tested both sides after five
  kinds showed placeholders to real people.
- **No email, push or SMS, and no preferences.** Email came with specs/060 for the buyer's notices.
- **No retention.** `notifications` grows without bound.
- **Up to 30 seconds of delay** on the bell, by design.
