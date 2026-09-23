# Implementation Plan: In-app notifications

**Branch**: `042-in-app-notifications` | **Spec**: [spec.md](spec.md)

## Technical Context

- **Contract** `Activity/UserNotificationRequested(NotificationId, RecipientId, Kind, Data, Link, OccurredAt)`.
- **Shared** `Notifications/INotifier` (+ `NotificationKind`), published through the caller's outbox like
  `IAuditTrail` - before the one save, or inside a repository `stage`.
- **Activity**: `notifications` (id PK, recipient, kind, data `jsonb`, link, created, read at; index on
  recipient + created); consumer inserts `ON CONFLICT DO NOTHING`; `GET /api/notifications`
  (`unreadOnly`, paging), `GET /api/notifications/unread-count`, `POST /api/notifications/{id}/read`,
  `POST /api/notifications/read-all` - all for the caller only.
- **Order**: `TrySettleAsync` takes a `stage`; `GetNoticeFactsAsync` (buyer, total, currency, parts with
  seller); `OrderNotices` sends: paid, failed, shipped, cancelled (customer); new sale, sale cancelled,
  received, payout (sellers). Audit `OrderPaid` joins the settle too.
- **Gateway**: `/api/notifications/**`.
- **Client**: `services/notifications`, `hooks/notifications` (30 s polling), `components/layout/notification-bell`,
  `pages/notifications`, `utils/notifications` (kind → words + link), `notifications` locale namespace.

## Research

- **D1 - Kind + data, not text.** A sentence stored in the language of the moment would stay in it; the
  storefront words it from `Kind` and `Data` in whatever language the reader has now.
- **D2 - Same transaction as the change.** Settling, moving a parcel, confirming a delivery, cancelling
  and paying out are guarded statements in their own transactions; the notification is staged there.
- **D3 - Polling.** 30 s on the unread count only; the list loads when the bell opens.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I | Activity owns the inboxes; Order only publishes. |
| III | Staged with the change; idempotent by id. |
| IV | Recipient from the event; reader from the token; nobody reads another's. |
| V | Tests for each event, idempotence, ownership, marking; client tests for the bell and wording. |
