# Message Contracts: An audit log of who did what

> Written on 2026-09-27, after the feature merged (#93), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

One new message. No existing message changed.

## `AuditEntryRecorded` - `Ecommerce.Contracts.Activity`

```csharp
public record AuditEntryRecorded(
    Guid EntryId,          // minted by the publisher; the entry's key
    string Category,       // AuditCategory: System, Security, User, Catalog, Order, Payment, Moderation
    string Action,         // PascalCase: ProductUpdated, PayoutRecorded
    Guid? ActorId,         // null: the system
    string? ActorEmail,
    string? ActorRole,     // the most powerful role held
    string SubjectType,
    string? SubjectId,
    string Summary,
    string? Before,        // JSON, secrets redacted; null for a creation
    string? After,         // JSON, secrets redacted; null for a deletion
    string Service,
    DateTime OccurredAt);
```

**Publishers**: Identity, Catalog, Inventory, Order and Payment, through `IAuditTrail.RecordAsync` in
`Ecommerce.Shared/Audit`, which publishes on the service's `IPublishEndpoint` - so through its EF outbox,
**before the one `SaveChangesAsync`**, or inside a repository's `stage` callback (research D7).

**Consumer**: Activity's `RecordAuditEntryConsumer` (endpoint names prefixed `ActivitySvc`), which sends
`RecordAuditEntryCommand`: compute the diff, then `INSERT ... ON CONFLICT ("Id") DO NOTHING`.

**Idempotency**: the primary key on `EntryId`, plus the EF inbox on the consumer endpoint. A redelivery
inserts nothing.

## Actions published at the merge

| Service | Category | Action |
| :-- | :-- | :-- |
| Identity | Security | `SignedIn`, `SignInRefused` |
| Identity | User | `Registered`, `ShopOpened`, `ShopRenamed`, `AddressAdded`, `AddressUpdated`, `AddressDeleted` |
| Catalog | Catalog | `CategoryCreated`, `CategoryDeleted`, `ProductCreated`, `ProductDeleted`, `ProductTextEdited`, `PriceSet`, `PriceRemoved`, `VariantAdded`, `VariantUpdated`, `ProductImageSet`, `ProductImageRemoved`, `VariantImageSet`, `VariantImageRemoved` |
| Catalog | System | `OrphanImagesReclaimed` |
| Inventory | Catalog | `StockSet` |
| Inventory | System | `ReservationsExpired` |
| Inventory | Order | `StockReturned` |
| Order | Order | `OrderPlaced`, `OrderCancelled`, `ParcelPrepared`, `ParcelShipped`, `ParcelReceived` |
| Order | System | `DeliveriesAutoConfirmed` |
| Order | Payment | `PayoutRecorded` |
| Payment | Payment | `PaymentCharged`, `PaymentRefused`, `RefundRecorded` |

Cart and the Orchestrator publish none.

## Delivery guarantees

| Property | Where it is handled |
| :-- | :-- |
| Same entry delivered twice | Primary key + `ON CONFLICT DO NOTHING`; EF inbox |
| Change rolled back | The entry was in the same outbox transaction and never left |
| Activity down | Entries wait in each publisher's outbox and the broker; no change is refused |
| Entries arrive out of order | Harmless: the list sorts by `OccurredAt`, set by the publisher |
