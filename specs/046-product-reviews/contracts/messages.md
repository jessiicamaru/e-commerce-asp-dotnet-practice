# Message Contracts: Ratings and reviews

> Written on 2026-09-27, after the feature merged (#98), from the code at that merge, the pull request and docs/features/ratings-and-reviews.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

One new integration message, `ParcelDeliveredEvent`, published by Order and consumed by Catalog. Catalog also
publishes two existing messages for the first time from reviews: `AuditEntryRecorded` and
`UserNotificationRequested`. No gRPC contract changed.

---

## New: `ParcelDeliveredEvent` - `Ecommerce.Contracts.Order`

```csharp
public record ParcelDeliveredEvent(
    Guid OrderId,
    Guid ShipmentId,
    Guid BuyerId,
    List<Guid> ProductIds,
    DateTime DeliveredAt);
```

| Field | Source | Meaning |
| :-- | :-- | :-- |
| `OrderId` | `order_shipments.OrderId` | The order the parcel belongs to |
| `ShipmentId` | `order_shipments.Id` | The parcel - one event per parcel |
| `BuyerId` | `orders.UserId` | The customer who received it (taken from the token at checkout) |
| `ProductIds` | `order_items.ProductId` of lines whose `SellerId` equals the parcel's, distinct | **Product** ids, not variant ids (research D2) |
| `DeliveredAt` | `order_shipments.DeliveredAt` | When the parcel was taken as delivered |

**Publisher**: Order, through `ParcelDeliveries.AnnounceAsync` (in
`Orders/Commands/ConfirmDelivery/DeliveryCommands.cs`), called from two places, both inside the transaction that
marks the parcel delivered and both through Order's transactional outbox:

- `ConfirmDeliveryCommandHandler` - the customer's `POST /api/orders/{id}/shipments/{shipmentId}/received`. The
  `stage` callback runs only when the guarded `UPDATE` changed the row, so a repeated confirmation announces
  nothing.
- `AutoConfirmDeliveriesCommandHandler` - the `DeliveryConfirmationSweeper` after `Delivery:AutoConfirmDays` (7).
  The `stage` receives the ids the sweep locked and set (`FOR UPDATE SKIP LOCKED`), so it announces exactly those.

**Guarantee**: each parcel is announced at most once - by whichever path set its `DeliveredAt` - and never
without its delivery committing. A parcel whose lines match nothing (no line of that seller) produces no event.

**Consumer**: Catalog, `ReviewEligibilityConsumer` (queue name from the class name, under Catalog's `CatalogSvc`
endpoint prefix). It sends `RecordReviewEligibilityCommand(BuyerId, ProductIds, DeliveredAt)`, which runs, per
distinct product:

```sql
INSERT INTO review_eligibility ("ProductId", "CustomerId", "FirstDeliveredAt")
VALUES (@productId, @customerId, @deliveredAt)
ON CONFLICT ("ProductId", "CustomerId") DO NOTHING
```

**Idempotency**: by the primary key. A redelivered event, or the same product arriving in a second parcel,
inserts nothing. Catalog's endpoints also run MassTransit's EF consumer outbox (the inbox), configured for every
endpoint before this feature; the key does not depend on it. No reply is published.

Nothing else consumes the event at this merge.

---

## Published by Catalog for reviews (existing contracts)

Both are staged before the one save in `SaveAndRecomputeAsync`, so they commit with the review and the
recomputed average through Catalog's bus outbox, or not at all.

### `AuditEntryRecorded` - `Ecommerce.Contracts.Activity`, via `IAuditTrail`

| Action | Category | Subject | Before / after |
| :-- | :-- | :-- | :-- |
| `ReviewPosted` | `Catalog` | `Review`, the review id | after `{ Rating, Body }` |
| `ReviewEdited` | `Catalog` | `Review` | before and after `{ Rating, Body }` |
| `ReviewHidden` | `Moderation` | `Review` | `{ Hidden: false }` → `{ Hidden: true, Reason }` |
| `ReviewRestored` | `Moderation` | `Review` | `{ Hidden: true, Reason }` → `{ Hidden: false }` |

Consumer: Activity, idempotent on `EntryId` (specs/041).

### `UserNotificationRequested` - `Ecommerce.Contracts.Activity`, via `INotifier`

Kind `NewReview` (new constant in `NotificationKind`), to the product's `SellerId`, with data
`{ "product": <product name>, "rating": "<1-5>" }` and link `/products/{productId}`. Sent only when a review is
first written and only for a seller's product - never on an edit, never for the shop's own products.

Consumer: Activity, idempotent on `NotificationId` (specs/042). The storefront words it from
`locales/{en,vi}/notifications.json` - English: `Somebody gave “{{product}}” {{rating}} stars.`

---

## Delivery guarantees the consumer must survive

| Property | Where it is handled |
| :-- | :-- |
| Same event delivered twice | `ON CONFLICT DO NOTHING` on (`ProductId`, `CustomerId`), plus the consumer inbox |
| Same product in two parcels | Same key; `FirstDeliveredAt` keeps the first |
| Customer confirmation racing the sweep | Order: the sweep's `FOR UPDATE SKIP LOCKED` plus the `DeliveredAt IS NULL` guard - one path sets the row and only that path announces |
| Event arriving after the customer opens the product page | The page asks `GET .../reviews/mine` again on the next load; eligibility only grows (research D1) |
| Product deleted before the event arrives | The row is inserted anyway (no foreign key); no review can be written for a missing product |

## Not being added

No `ReviewPostedEvent` or rating event for other services: nothing outside Catalog needs reviews at this merge.
