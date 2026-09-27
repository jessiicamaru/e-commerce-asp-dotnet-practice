# Message contract: A shop has a page

## `Ecommerce.Contracts.Identity.SellerDescribedEvent`

```csharp
public record SellerDescribedEvent(Guid SellerId, string? Description, DateTime DescribedAt);
```

- **Publisher**: Identity's `DescribeShopCommandHandler`. It stages the event before the one `SaveChangesAsync`, together
  with the profile change and its audit entry, so all three commit together through the transactional outbox.
- **Consumer**: Catalog's `SellerDescribedConsumer`, which sends `RecordShopDescriptionCommand`. That command runs the
  guarded upsert in [data-model.md](../data-model.md).
- **Ordering**: no delivery order is assumed. `DescribedAt` (the profile's `UpdatedAt`) decides, so an older or
  redelivered event changes nothing.
- A null `Description` means the seller cleared it.
