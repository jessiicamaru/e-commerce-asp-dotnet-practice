# Message Contracts: Delete a Product

> Written on 2026-09-27, after the feature merged (#61), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

One new integration event. It was added to `Ecommerce.Contracts` together with its publisher and its
consumer, as the contracts rule requires.

---

## Published by Catalog

### `ProductDeletedEvent` - `Ecommerce.Contracts.Catalog`

```csharp
public record ProductDeletedEvent(
    Guid ProductId,
    IReadOnlyList<Guid> VariantIds,
    DateTime DeletedAt
);
```

| Field | Meaning |
| :--- | :--- |
| `ProductId` | The product that was deleted |
| `VariantIds` | **Every** variant of it, collected before the delete. The first reuses the product's id; later ones do not (specs/020) |
| `DeletedAt` | `DateTime.UtcNow` when the handler ran |

**Publisher**: `DeleteProductCommandHandler`, through Catalog's EF Core transactional outbox: the
removal is staged, the event is published, then `SaveChangesAsync` runs once. A crash cannot leave a
deleted product nobody was told about, or an announcement about a product still there.

**Meaning**: the rows are gone because they should never have existed. It is **not** "stop selling
it" - deactivation publishes nothing of the kind.

---

## Consumed by Inventory

### `ProductDeletedConsumer` → `ForgetProductCommand(VariantIds)`

**Behaviour**: deletes the `stock_items` rows whose `ProductId` (a variant id since specs/020) is in
`VariantIds`, in one statement, and logs how many of how many were forgotten. An empty list touches
nothing.

**Idempotency**: by construction. A redelivery runs the same `DELETE` and affects zero rows; zero is
logged as a normal answer. Inventory's receive endpoints also use MassTransit's inbox, as every
consumer there does. Asserted by `Forgetting_twice_is_a_no_op_because_a_message_redelivers`.

**Registration**: `x.AddConsumer<ProductDeletedConsumer>()` in Inventory's `Program.cs`. The consumer was
first written without this line and never ran; see [research.md D5](../research.md).

**Queue name**: derived from the class name, `ProductDeleted`. No other service has a consumer of that
name, so it does not share a queue.

---

## Not told

| Service | Why not |
| :--- | :--- |
| Order | Every order froze the name, price, SKU and option summary of what it bought |
| Cart | A line whose variant Catalog no longer has is already shown as `NoLongerAvailable` |
| Orchestrator | The saga never refers to a product by anything but the ids on the order |
