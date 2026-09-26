# Contracts: A banned seller's shop is closed

**Feature**: [spec.md](../spec.md)

## New message - `Ecommerce.Contracts.Identity.SellerSuspensionChangedEvent`

```csharp
public record SellerSuspensionChangedEvent(Guid SellerId, bool Suspended, DateTime ChangedAt);
```

| | |
| :-- | :-- |
| Published by | Identity - `BanUserCommand` (`Suspended: true`) and `LiftBanCommand` (`false`), only when the user holds `Seller`; through the outbox, before the one save |
| Consumed by | Catalog - `SellerSuspensionChangedConsumer` → `RecordSellerSuspensionCommand` |
| Ordering | `ChangedAt` is the guard; a message older than what is stored changes nothing |
| Idempotency | a redelivery carries the same `ChangedAt` and is not newer, so it changes nothing and tells nobody |

One message with a flag rather than two: both directions share one guard, and one consumer class means one queue.

## HTTP changes

| Endpoint | Change |
| :-- | :-- |
| `POST /api/shop-applications/{id}/approve` (Staff) | **409** "The applicant is banned." when the applicant is banned |
| `GET /api/products`, `GET /api/products/{id}`, images, reviews, questions (public) | a suspended seller's products answer as any product off the shelf (absent / 404) |
| gRPC `CatalogPricing` | `Sellable = false` for a suspended seller's variants |

No response shape changes.

## Bruno

`admin-users/`: an administrator bans the folder's seller... - the collection's seller runs in `seller/` (seq 16), after
`admin-users/`; the check goes in `seller/` after its product is on sale: ban the seller, the product is 404 to the public,
lift the ban, it is back.
