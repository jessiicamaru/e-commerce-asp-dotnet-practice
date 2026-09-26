# Phase 1 Data Model: In-app notifications

> Written on 2026-09-27, after the feature merged (#94), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One new table in `ecommerce_activity_db` (host port 5440), added by migration
`20260923195702_AddNotifications` in
`server/src/Services/Activity/Ecommerce.Activity.Infrastructure/Migrations/`. No table changed in any other
service. The MassTransit inbox and outbox tables Activity's consumers use were added by specs/041's
`20260923192252_InitialCreate` and are unchanged.

---

## `notifications`

One row per thing one person was told. Written only by `RecordNotificationConsumer` (through
`NotificationRepository.TryAddAsync`); read and marked only by its recipient.

Entity: `Ecommerce.Activity.Domain.Entities.Notification`. Mapping:
`Persistence/Configurations/NotificationConfiguration.cs`.

| Column | Type (PostgreSQL) | Constraints | Meaning |
| :-- | :-- | :-- | :-- |
| `Id` | `uuid` | PK `PK_notifications`; `ValueGeneratedNever()` | The publisher's `NotificationId`. A redelivery is the same row (research D5) |
| `RecipientId` | `uuid` | Not null | Who it is for. Always from the published message, never from a request |
| `Kind` | `character varying(64)` | Not null | One of the `NotificationKind` constants, e.g. `OrderPaid` |
| `Data` | `jsonb` | Not null | A JSON object of strings - what the words need (`orderId`, `total`, `currency`, `tracking`, `shop`, `by`, `amount`) |
| `Link` | `character varying(256)` | Nullable | Where it points in the storefront, e.g. `/orders/{id}` |
| `CreatedAt` | `timestamp with time zone` | Not null | The message's `OccurredAt` - when it happened, not when Activity stored it |
| `ReadAt` | `timestamp with time zone` | Nullable | Null until the recipient reads it; set once |

**Indexes**:

| Name | Columns | Filter | Serves |
| :-- | :-- | :-- | :-- |
| `IX_notifications_RecipientId_CreatedAt` | `(RecipientId, CreatedAt)` | none | The inbox, newest first (`GetPageAsync`) |
| `IX_notifications_unread` | `(RecipientId)` | `"ReadAt" IS NULL` | The bell's count, asked every 30 seconds, and mark-all-read |

No check constraint on `Kind`: the list of kinds grows with later features (Identity and Catalog added theirs),
and a kind unknown to a reader is still shown (research D9). No foreign key to a user: Activity holds no users;
the recipient id is Identity's.

`Data` is stored as `jsonb` and read back as `Dictionary<string, string>`; the handler serialises the message's
dictionary with `System.Text.Json`, and the insert casts it (`CAST({Data} AS jsonb)`).

---

## Writes, and the guard in each

Every write is one statement, and every statement names what makes it safe to repeat:

| Operation | Statement | Guard |
| :-- | :-- | :-- |
| Record | `INSERT INTO notifications (...) VALUES (...) ON CONFLICT ("Id") DO NOTHING` | The primary key. Returns 1 for the delivery that inserted, 0 for every repeat |
| Mark one read | `UPDATE notifications SET "ReadAt" = @at WHERE "Id" = @id AND "RecipientId" = @me AND "ReadAt" IS NULL` (EF `ExecuteUpdateAsync`) | The recipient and `ReadAt IS NULL` in the `WHERE`. Zero rows is then told apart by one `EXISTS` on id + recipient: already read (fine) or not theirs (404) |
| Mark all read | `UPDATE notifications SET "ReadAt" = @at WHERE "RecipientId" = @me AND "ReadAt" IS NULL` | Returns how many it marked; a repeat marks 0 |

---

## State transitions

```text
   UserNotificationRequested ──▶  unread (ReadAt NULL) ──▶ read (ReadAt set)
                                        │   mark one read, or mark all read
                                        │   (only the recipient; guarded on ReadAt IS NULL)
```

`read` has no outgoing transition: there is no "mark unread" and no deletion. A second mark affects zero rows.

---

## What changed in Order, and what did not

No migration in Order. The feature reads existing columns only:

- `GetNoticeFactsAsync` reads `orders.UserId`, `TotalAmount`, `Currency`, the order's `order_shipments`
  (`Id`, `SellerId`) and each line's `order_items.SellerId` / `SellerName` (specs/034, 035, 036). It calls
  `EnsureShipmentsAsync` first, which may **insert** missing parcel rows for an order an older image wrote -
  the same on-demand creation every parcel move already does (specs/035).
- `orders.Currency` may be null on orders from before specs/022; the facts then carry an empty currency.

Two audit actions were added to Order's settle while its `stage` was being built, recorded through the
existing `audit_entries` path with no schema change: `OrderPaid` and `OrderFailed` (category Order, actor the
system). The storefront gained labels for both in `locales/*/admin.json`.

---

## Transaction rules

1. The notice's outbox row is written in the transaction of the change it announces - before the one
   `SaveChangesAsync`, or inside the repository's `stage` (research D2).
2. `stage` runs only when the guarded statement affected a row, so a repeat stages nothing.
3. Inside a consumer, `TrySettleAsync` joins the transaction MassTransit's consumer outbox already holds; it
   opens its own only when none is open, inside `CreateExecutionStrategy().ExecuteAsync` (research D6).
4. Activity's insert is its own statement inside the consumer's transaction with MassTransit's inbox row, so a
   crash between them rolls both back and the broker redelivers.

---

## Schema compatibility

Additive only: a new table and two indexes. An Activity image from before this migration runs against the new
schema unchanged (it never reads the table); an Order image from before this feature publishes no notices and
is otherwise unaffected. The `Down` migration drops the table - a recovery, not a rollback path (constitution,
"Schema evolution"). The table has not been altered since; specs/078's `notification_wording_versions` is a
separate table.
