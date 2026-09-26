# Message Contracts: Cart removal by variant

> Written on 2026-09-27, after the feature merged (#134), from the code at that merge, the pull request and
> docs/features/shopping-and-checkout.md.

**Feature**: [spec.md](../spec.md)

**No contract changed.** Cart now reads a field that was already on the message. No HTTP endpoint and no gRPC
contract changed either.

## Consumed: `OrderSubmittedEvent` - `Ecommerce.Contracts.Order`

```csharp
record OrderSubmittedEvent(Guid OrderId, Guid UserId, decimal TotalAmount, List<OrderItemDto> Items,
                           DateTime CreatedAt, string Currency = "");
record OrderItemDto(Guid ProductId, int Quantity, decimal UnitPrice, Guid VariantId = default);
```

**Publisher**: Order, through its transactional outbox, at submission.

**Consumer in Cart**: `OrderSubmittedConsumer` → `CheckoutOutcomes.RecordSubmittedAsync`.

**What changed in the consumer**: it maps each item with `OrderedItem.From`, keeping `VariantId` (since
specs/020 on the message; dropped by Cart until this feature). `UnitPrice` and `Currency` are still ignored -
the cart stores no price.

**Idempotency**: unchanged - the `checkout_outcomes` row is locked `FOR UPDATE`, and the removal is applied once,
guarded by `Applied` (specs/010).

## Consumed: `OrderCompletedEvent`, `OrderFailedEvent`

Unchanged. `OrderCompletedEvent` (order id only) is what triggers the removal; whichever of it and the
submission arrives second applies it.

## Legacy senders

| Sender | `VariantId` on arrival | Read as |
| :--- | :--- | :--- |
| Order at or after specs/020 | the variant bought | that variant |
| Order older than specs/020 | `Guid.Empty` | the product id - the first variant's id |
| An item already stored in `checkout_outcomes` before this deploy | absent in the JSON → `Guid.Empty` | the product id |
