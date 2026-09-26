# Message Contracts: Two Price Lists

> Written on 2026-09-27, after the feature merged (#59), from the code at that merge, the pull request, docs/features/catalog.md and docs/features/shopping-and-checkout.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md) D4

No new message. Two existing records gained an optional field, and the saga that relays one into the other
was changed to carry it.

---

## `OrderSubmittedEvent` - `Ecommerce.Contracts.Order`

```csharp
public record OrderSubmittedEvent(
    Guid OrderId,
    Guid UserId,
    decimal TotalAmount,
    List<OrderItemDto> Items,
    DateTime CreatedAt,
    string Currency = ""        // new: what TotalAmount is in; "" = the shop's default
);
```

**Publisher**: Order's `SubmitOrderCommandHandler`, through Order's outbox, with the currency the order froze.

**Consumers**: the Orchestrator's `OrderStateMachine` (stores `Currency` on `OrderStateData`) and Cart's
`OrderSubmittedConsumer` (reads the items, ignores the currency). Nothing else consumes the event.

**Asserted by**: `OrderCurrencyTests.The_currency_travels_with_the_amount_to_the_saga`.

---

## `ProcessPaymentCommand` - `Ecommerce.Contracts.Payment`

```csharp
public record ProcessPaymentCommand(
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Currency = ""        // new: what Amount is in; "" = the shop's default
);
```

**Publisher**: the Orchestrator, on entering the payment step, with `context.Saga.Currency ?? string.Empty`
- relayed, never re-derived.

**Consumer**: Payment's `ProcessPaymentConsumer` → `ChargeOrderCommand(..., Currency)`, which writes
`payments.Currency` as sent, or the configured default for `""`.

---

## Compatibility

| Situation | Result |
| :--- | :--- |
| An Order image from before this feature publishes the event | No `Currency`; the saga stores an empty or null currency and relays `""`; Payment records the default - which is what that order meant |
| A message in flight during the deploy | Same as above |
| An Orchestrator image **not rebuilt** | Deserialises into its older record, drops the field, and publishes a `ProcessPaymentCommand` with no currency: Payment records the default for an order placed in dollars. The specs/020 `VariantId` defect on the field where it cannot be detected afterwards - which is why the quickstart insists on `--build` |

## Idempotency

Unchanged. The saga correlates on `OrderId` with optimistic concurrency, and Payment records one payment per
order under the existing unique index on `payments.OrderId`; neither decision depends on the new field.

## Not given a currency

`OrderCompletedEvent`, `OrderFailedEvent`, `PaymentProcessedEvent`, `PaymentFailedEvent` and every Inventory
message carry no amount, so they carry no currency.
