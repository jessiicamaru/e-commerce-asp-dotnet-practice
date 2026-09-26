# Message Contracts: Following One Order Across Seven Services

> Written on 2026-09-27, after the feature merged (#33), from the code at that merge, the pull request
> and docs/guides/observability.md (this feature has no page under docs/features/).

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md D2, D4](../research.md)

**No message record changed.** Nothing in `Ecommerce.Contracts` was edited, and no publisher, consumer
or queue was added or renamed.

## Trace context rides in the headers

MassTransit already writes the current W3C trace context into each message's headers when it sends or
publishes, and restores it when it consumes. This feature adds `AddSource("MassTransit")` so those
send, publish, consume and saga activities are exported - which is what makes one checkout one trace
across the broker. The bodies are unchanged.

## `OrderId` on the consuming side

`OrderIdLogScopeFilter<T>` is registered on six buses (Cart, Catalog, Inventory, Orchestrator, Order,
Payment) with `cfg.UseConsumeFilter(typeof(OrderIdLogScopeFilter<>), context)`. For each consumed
message it reads a public `Guid OrderId` property, if the type has one, then:

- tags the current span `order.id`, and
- opens a logging scope `{ OrderId = <id> }` around the rest of the pipeline.

Messages without such a property pass through untouched. The contracts this reaches are every order
message in `Ecommerce.Contracts` - `OrderSubmittedEvent`, `ReserveInventoryCommand`,
`InventoryReservedEvent`, `InventoryReservationFailedEvent`, `ProcessPaymentCommand`,
`PaymentProcessedEvent`, `PaymentFailedEvent`, `ReleaseInventoryCommand`, `OrderCompletedEvent`,
`OrderFailedEvent` - because each names its id `OrderId`. A new order contract that named it anything
else would silently drop out of the `OrderId` query.

## The saga's replies with no instance

For `InventoryReservedEvent`, `InventoryReservationFailedEvent`, `PaymentProcessedEvent` and
`PaymentFailedEvent`, `OrderStateMachine` now declares `OnMissingInstance`: the reply is still
**discarded** (redelivering it would fault for ever), and a Warning is logged:

```text
Saga {OrderId}: {Event} arrived but no saga instance exists for it, so it was discarded. If the order
is still Submitted, the reply overtook the submission's commit - check that the orchestrator publishes
through the transactional outbox.
```

## Idempotency

Unchanged. The filter only adds a scope and a tag; it neither acknowledges nor redelivers anything.
