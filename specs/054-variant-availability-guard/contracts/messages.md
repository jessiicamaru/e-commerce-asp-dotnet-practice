# Message Contracts: Variant availability guard

> Written on 2026-09-27, after the feature merged (#137), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](../spec.md)

**No contract changed.** The feature changes how Catalog records one existing message. No HTTP endpoint and no
gRPC contract changed.

## Consumed: `StockAvailabilityChangedEvent` - `Ecommerce.Contracts.Inventory`

```csharp
public record StockAvailabilityChangedEvent(Guid ProductId, int QuantityAvailable, bool IsAvailable,
                                            DateTime ObservedAt, Guid VariantId = default);
```

**Publisher**: Inventory, through its outbox, from every path that moves stock (`StockAvailabilityAnnouncer`).

**Consumer in Catalog**: `StockAvailabilityChangedConsumer` → `RecordStockAvailabilityCommand(ProductId,
IsAvailable, ObservedAt, VariantId)`; a `Guid.Empty` variant falls back to the product id (specs/020).
`QuantityAvailable` is deliberately not stored.

**Ordering and idempotency** - the rule this feature restores:

| Arriving `ObservedAt` vs recorded | Effect |
| :--- | :--- |
| none recorded | recorded |
| later | recorded, **whatever the value** (the fix) |
| equal | nothing - a duplicate |
| earlier | nothing - overtaken in flight |

A recorded announcement recomputes the product's rollup. A zero-row result is logged: Information when the
variant exists (ordinary), Warning when Catalog does not hold it (discarded, not retried).
