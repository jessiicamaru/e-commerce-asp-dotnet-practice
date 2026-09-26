# Message Contracts: A shopper saves a product for later

> Written on 2026-09-27, after the feature merged (#159), from the code at that merge, the pull request and docs/features/saved-products.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

**No message contract was added or changed.** The feature consumes one existing event and publishes one existing
message with a new `Kind` value. Nothing in `Ecommerce.Contracts` was edited, so no relaying service had to be
rebuilt.

---

## Consumed (unchanged)

### `StockAvailabilityChangedEvent` - `Ecommerce.Contracts.Inventory`

```csharp
record StockAvailabilityChangedEvent(Guid ProductId, int QuantityAvailable, bool IsAvailable, DateTime ObservedAt,
                                     Guid VariantId = default);
```

Published by Inventory (the stock announcer, specs/004 and specs/020); consumed in Catalog by
`StockAvailabilityChangedConsumer`, which sends `RecordStockAvailabilityCommand`. Unchanged by this feature, as is
the consumer.

**What changed is the handler behind it**,
[RecordStockAvailabilityCommandHandler.cs](../../../server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Availability/RecordStockAvailabilityCommandHandler.cs):

1. The variant's availability is recorded by the existing time-guarded update (specs/054). An announcement older
   than what is held affects zero rows and ends there - so a redelivery or an overtaken announcement cannot reach
   step 2.
2. If it recorded something, `RecomputeProductRollupAsync` recomputes the product's rollup and now returns whether
   **this** statement flipped `Availability` from false to true (research D4).
3. Only on that flip, and only if the product is listed and active, each saver is told (below). Otherwise nobody.

---

## Published

### `UserNotificationRequested` - `Ecommerce.Contracts.Activity`, kind `SavedBackInStock`

```csharp
record UserNotificationRequested(Guid NotificationId, Guid RecipientId, string Kind,
                                 Dictionary<string, string> Data, string? Link, DateTime OccurredAt);
```

Published by Catalog through `INotifier.NotifyAsync` (specs/042), one per saver of the product:

| Field | Value |
| :-- | :-- |
| `NotificationId` | `Guid.CreateVersion7()`, minted by `Notifier` - the notification's key at Activity |
| `RecipientId` | The saver's `CustomerId` from `saved_products` (`SaverIdsAsync(productId)`) |
| `Kind` | `SavedBackInStock` (`NotificationKind.SavedBackInStock`) |
| `Data` | `{ "product": <product.Name> }` - the product's own (default-language) name. Declared in `notification-kinds.json` as `"SavedBackInStock": { "required": ["product"] }` |
| `Link` | `/products/{productId}` |
| `OccurredAt` | `DateTime.UtcNow` at publication |

**Consumer**: Activity's `RecordNotificationConsumer`, which keeps it in the recipient's inbox, idempotent on
`NotificationId` (specs/042). The storefront words it from `kind.SavedBackInStock` in
`client/src/locales/{vi,en}/notifications.json`:

- en: `“{{product}}”, which you saved, is back in stock`
- vi: `“{{product}}” bạn đã lưu đã có hàng trở lại`

**Atomicity (Constitution III)**: the notices are published inside the handler that the consumer runs, and
Catalog's consumers run with `UseEntityFrameworkOutbox<CatalogDbContext>` - so the notices are written to the
outbox in the consumer's transaction, together with the availability they announce. A failed consume rolls both
back and the broker redelivers.

**Idempotency**: a redelivered `StockAvailabilityChangedEvent` finds the variant's observation already recorded
(step 1 affects zero rows) and publishes nothing. A second announcement that the product is still available finds
`Was = true` and publishes nothing. Two concurrent statements cannot both report the flip, because each reads the
value its own statement started from (research D4). Activity additionally keeps one row per `NotificationId`.

**Who is not told**:

- the savers of a product that is not listed or not active when it comes back (research D3);
- anybody at all when availability goes true to true, true to false, or false to false;
- anybody when the rollup is recomputed by a price change, an added variant or an edited variant - those three
  callers discard the returned flip (see [plan.md](../plan.md), "What this feature does not finish").

---

## Later: an email goes with it (specs/083)

Not part of this feature, recorded so the reader of this file is not surprised: since specs/083 (PR #171) the same
loop also calls `IEmailSender.SendAsync(saver, EmailTemplate.SavedBackInStock, { productId, product },
EmailTemplate.ReadersLanguage)`, which publishes through the same outbox and is sent by Identity in the saver's
language. See [specs/083-more-emails](../../083-more-emails/).
