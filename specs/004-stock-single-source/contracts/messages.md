# Message Contracts: One Source of Truth for Stock

**Feature**: [spec.md](../spec.md) | **Date**: 2026-09-17

**This feature adds one record to `Ecommerce.Contracts`.** That is a breaking change by this
project's own rule — every service deserializes these records — so it gets its own document rather
than a line in the plan.

---

## New: `StockAvailabilityChangedEvent`

Lives in `Ecommerce.Contracts/Inventory/`, beside the reservation messages Inventory already owns.

```csharp
public record StockAvailabilityChangedEvent(
    Guid ProductId,
    int QuantityAvailable,
    bool IsAvailable,
    DateTime ObservedAt
);
```

| Field | Why it is here |
| :--- | :--- |
| `ProductId` | What the announcement is about. Not the SKU — the catalogue keys on the id |
| `QuantityAvailable` | The owner already has it, and a future subscriber (a low-stock alerter, a report) would otherwise need a second contract. Catalog **does not store it** |
| `IsAvailable` | `QuantityAvailable > 0`, derived by the owner. Subscribers must not re-derive it — if the definition of "available" ever changes, it changes in one place |
| `ObservedAt` | When Inventory observed this. **Not** when the message was sent or received. It is what makes an overtaken announcement losable (research D3) |

**`Catalog` storing `QuantityAvailable` would recreate the defect this feature deletes**, one
release later and with a sync mechanism that makes it look defensible. The field is on the message
for other subscribers; it is not for the catalogue.

---

## Published by

`Ecommerce.Inventory`, from **all six** Application handlers that move stock. Every one stages its
change, publishes, then calls `SaveChangesAsync` once.

| Handler | What moved | Typical transition |
| :--- | :--- | :--- |
| `RegisterProductCommandHandler` | a stock item exists now, at zero | → `IsAvailable = false` |
| `SetStockOnHandCommandHandler` | an administrator set the level | either direction |
| `ReserveStockCommandHandler` | units held for an order | often `true → false` on the last units |
| `ReleaseStockCommandHandler` | a hold returned to the shelf | often `false → true` |
| `ConfirmStockCommandHandler` | units left the building | stays `false`, or `true → false` |
| `ExpireStockCommandHandler` | the sweeper returned an abandoned hold | often `false → true` |

`RegisterProduct` announcing a zero is not redundant. It is the announcement that turns "never told"
into "told, and the answer is no", and without it a product's first real movement is its first
announcement — which is fine for the listing but hides the fact that a path was forgotten.

**Six publish sites is the risk in this feature**, and it is mitigated by a test per site rather
than by being careful. See research D2 for the interceptor that would have removed the risk and why
it was not taken.

---

## Consumed by

`Ecommerce.Catalog` — its **first consumer**. Nothing else subscribes today.

Effect: the guarded, time-compared update in [data-model.md](../data-model.md). Zero rows affected
is a normal outcome, not an error.

Because it is Catalog's first consumer, Catalog needs the same three lines Order needed in feature
003:

```csharp
x.AddConsumer<StockAvailabilityChangedConsumer>();

x.AddConfigureEndpointsCallback((context, _, cfg) =>
    cfg.UseEntityFrameworkOutbox<CatalogDbContext>(context));

// Not optional. Feature 003 shipped a defect where Inventory and Order both had a class called
// OrderCompletedConsumer, bound to one queue named OrderCompleted, and COMPETED for it - each
// completion reached one service instead of both. Queue names come from consumer class names.
x.SetEndpointNameFormatter(new DefaultEndpointNameFormatter(prefix: "CatalogSvc", includeNamespace: false));
```

**The inbox needs no migration — verified, not assumed:**

```bash
$ grep -o "InboxState\|OutboxState\|OutboxMessage" \
    server/src/Services/Catalog/.../Migrations/20260902161405_AddMassTransitOutbox.cs | sort -u
InboxState
OutboxMessage
OutboxState
```

The tables have been in `ecommerce_catalog_db` since September 2nd, because `CatalogDbContext`
already calls `AddTransactionalOutboxEntities()`. Only the endpoint callback is missing — the same
situation Order was in, and the same reason: until now Catalog had no consumers.

---

## Unchanged

- `ProductCreatedEvent` — **does not gain an opening quantity.** That was the rejected alternative
  in the spec's clarification: it would be one step for an administrator at the cost of the
  catalogue telling the stock owner what its stock is, which is the ownership inversion this feature
  exists to remove.
- `ReserveInventoryCommand`, `InventoryReservedEvent`, `InventoryReservationFailedEvent`,
  `ReleaseInventoryCommand` — the checkout path is untouched. Reservation still happens against
  Inventory's row under `FOR UPDATE`; nothing sells against the catalogue's copy.

---

## Delivery guarantees relied on

| Guarantee | Provided by | Notes |
| :--- | :--- | :--- |
| At-least-once delivery | RabbitMQ + MassTransit | Redelivery is normal operation |
| Atomicity of change and announcement | The EF outbox, publish before the single `SaveChangesAsync` | Constitution III |
| Duplicate suppression | `InboxState` on Catalog's receive endpoints | An optimisation, not the guarantee |
| Correct behaviour when duplicates *or reordering* get through | The time-compared `UPDATE` | **This is the guarantee.** Tests must pass with the inbox removed |

Ordering is **not** relied on. RabbitMQ preserves per-queue order only in the absence of redelivery,
and redelivery is the normal case here.
