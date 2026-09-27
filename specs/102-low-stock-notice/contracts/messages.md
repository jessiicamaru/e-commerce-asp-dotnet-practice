# Message contract: A seller is told when a variant runs low

## `Ecommerce.Contracts.Inventory.StockRanLowEvent` (new)

```csharp
public record StockRanLowEvent(Guid VariantId, int QuantityAvailable, int Threshold, DateTime OccurredAt);
```

- **Publisher**: Inventory's `ReserveStockCommandHandler`, staged through the outbox in the reservation's own
  transaction, only for a variant that crossed (data-model.md).
- **Consumer**: Catalog's `StockRanLowConsumer`. It uses the endpoint's consumer outbox, whose inbox handles
  redelivery.
- **Idempotency**: the publisher is covered by the reservation's own duplicate check (a redelivered
  `ReserveInventoryCommand` changes nothing and publishes nothing). The consumer is covered by its inbox.
- The consumer's name says what it does. No other service has a consumer of that name, so no queue is shared (the
  CLAUDE.md queue-name gotcha).

## `UserNotificationRequested`: new kind

- `StockRunningLow`, sent to the product's seller. Data is `{ "product": "Fujifilm X-T5 · Colour: Silver", "left": "4" }`,
  and the link is `/shop/products/{productId}`.
