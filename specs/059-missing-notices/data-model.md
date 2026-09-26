# Phase 1 Data Model: The notices nobody got

> Written on 2026-09-27, after the feature merged (#142), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md).

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration was added or changed.** The four kinds are new values in the existing
`notifications."Kind"` column (varchar(64)) in Activity, and their data goes in the existing `Data` column (jsonb). An older image
reads them as unknown kinds, which the storefront already shows generically.

---

## The four kinds

Declared in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json` and as constants on
`NotificationKind` in `Notifier.cs`:

| Kind | Required data | Recipient | Link | Sent by |
| :--- | :--- | :--- | :--- | :--- |
| `AccountLocked` | `until` (ISO 8601 UTC, `"o"` format), `reason` | the locked user | none | Identity, `UserAdministrationHandlers` (lock) |
| `AccountBanned` | `reason` | the banned user | none | Identity, `UserAdministrationHandlers` (ban) |
| `ParcelAutoDelivered` | `orderId` | the parcel's seller (`order_shipments.SellerId`); nobody when null | `/shop/sales/{orderId}` | Order, `AutoConfirmDeliveriesCommandHandler` |
| `ReviewHidden` | `product` (the product's name), `reason` | the review's author (`CustomerId`) | `/products/{productId}` | Catalog, `ReviewHandlers` (hide) |

## Rows written

| Table (service) | What | When |
| :--- | :--- | :--- |
| MassTransit outbox (Identity, Order, Catalog) | One `UserNotificationRequested` per notice | Lock and ban: before their one save. Sweep and hide: inside the guarded statement's transaction, through its `stage` callback |
| `notifications` (Activity, 5440) | One row per notice, `INSERT ... ON CONFLICT (Id) DO NOTHING` on `NotificationId` | When Activity consumes it (unchanged since specs/042) |

## Storefront wording

`client/src/locales/{en,vi}/notifications.json`, keys `kind.<Kind>`; the placeholders are filled by
`describeNotification`, where `order` is the first 8 characters of `orderId` and `until` is formatted with
`toLocaleString(language)`.
