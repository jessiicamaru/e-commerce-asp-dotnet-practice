# Message Contracts: Inventory Reservations

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

The inventory service is driven almost entirely by messages. Everything below except the last
section already exists in `Ecommerce.Contracts` and is **not** being redesigned.

---

## Consumed

### `ReserveInventoryCommand` — `Ecommerce.Contracts.Inventory`

```csharp
record ReserveInventoryCommand(Guid OrderId, List<OrderItemDto> Items);
record OrderItemDto(Guid ProductId, int Quantity, decimal UnitPrice);   // from Contracts.Order
```

Published by `OrderStateMachine` on entering `Submitted`.

**Behaviour**: reserve every line atomically, or none. Reply with exactly one of
`InventoryReservedEvent` or `InventoryReservationFailedEvent` — never both, never neither (FR-002).
`UnitPrice` is ignored; inventory has no opinion on price.

**Rejection reasons** (FR-004), each naming the offending product:

| Condition | Reason text |
| :--- | :--- |
| Not enough available | `Insufficient stock for product {id}: requested {n}, available {m}` |
| Product unknown to inventory | `Unknown product {id}` |
| Non-positive quantity | `Invalid quantity {n} for product {id}` |
| Empty item list | `Order contains no items` |

### `ReleaseInventoryCommand` — `Ecommerce.Contracts.Inventory`

```csharp
record ReleaseInventoryCommand(Guid OrderId, string Reason);
```

Published by the saga when payment fails. Releases every `Held` reservation for that order.

**Behaviour**: no reply message. Releasing an order that was never reserved, or is already settled,
succeeds silently and changes nothing (User Story 3 scenarios 2 and 3).

### `OrderCompletedEvent` — `Ecommerce.Contracts.Order`

```csharp
record OrderCompletedEvent(Guid OrderId, DateTime CompletedAt);
```

Published by the saga after payment succeeds.

**Behaviour**: confirm every `Held` reservation for that order — units leave stock permanently.

> This is the gap described in [research.md D2](../research.md). Nothing consumes this event today,
> so successful orders would leave their units held until the sweeper wrongly released them.

### `ProductCreatedEvent` — `Ecommerce.Contracts.Catalog`

```csharp
record ProductCreatedEvent(Guid ProductId, string Name, decimal Price, string Sku, Guid CategoryId, DateTime CreatedAt);
```

**Behaviour**: register a `stock_items` row at zero units. Already-registered products are left
untouched (FR-006 applies here too — this event redelivers like any other).

---

## Published

### `InventoryReservedEvent`

```csharp
record InventoryReservedEvent(Guid OrderId, DateTime ReservedAt);
```

Correlated by `OrderId`. Moves the saga from `Submitted` to `InventoryReservedState`.

### `InventoryReservationFailedEvent`

```csharp
record InventoryReservationFailedEvent(Guid OrderId, string Reason);
```

Correlated by `OrderId`. Finalizes the saga and produces `OrderFailedEvent`.

Both are published through the transactional outbox in the same transaction as the stock change, so
a reply can never exist without its stock effect, or vice versa.

---

## Delivery guarantees the consumers must survive

| Property | Where it is handled |
| :--- | :--- |
| Same message delivered twice | MassTransit EF inbox, plus the unique `(OrderId, ProductId)` constraint |
| Release arriving before reserve | Release finds no `Held` rows, does nothing; the later reserve proceeds normally, and the units are recovered by expiry |
| Confirm or release after expiry | Guarded status update matches nothing, so stock is untouched (FR-017) |
| Consumer crash mid-transaction | Database transaction rolls back; the broker redelivers |

---

## Not being added

`ConfirmInventoryCommand` was considered and rejected — see
[research.md D2](../research.md). Consuming `OrderCompletedEvent` achieves the same thing without
editing the saga or the shared contracts. If an explicit confirm is added later, it replaces the
`OrderCompletedEvent` consumer and nothing else.
