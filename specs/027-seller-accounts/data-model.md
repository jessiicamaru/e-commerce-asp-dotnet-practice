# Data Model: The Shop Is a Marketplace

> Completed on 2026-09-27, after the feature merged (#64), from the code at that merge, the pull request and docs/features/marketplace.md.

Two migrations: `20260922173126_AddSellerProfilesAndOutbox` in Identity and `20260922173905_AddSellers` in
Catalog. Both are additive (new tables, one nullable column), so an earlier image of either service runs
against the new schema.

## Identity

### `seller_profiles` (new)

| Column | Type | Null | Notes |
| :-- | :-- | :-- | :-- |
| `UserId` | `uuid` | no | PK **and** FK → `users`, cascade. One shop per account (spec assumption) |
| `ShopName` | `varchar(100)` | no | What a shopper sees under a product |
| `CreatedAt` | `timestamptz` | no | |
| `UpdatedAt` | `timestamptz` | no | |

The user id is the primary key rather than a surrogate: there is exactly one profile per user, and a
separate `Id` would let two exist.

Exact shape (from the migration): `UserId uuid NOT NULL`, PK `PK_seller_profiles`, FK
`FK_seller_profiles_users_UserId` → `users."Id"` `ON DELETE CASCADE`; `ShopName character varying(100) NOT
NULL`; `CreatedAt` and `UpdatedAt` `timestamp with time zone NOT NULL`.

### MassTransit outbox tables (new in Identity)

The same migration adds `InboxState`, `OutboxState` and `OutboxMessage` - what
`modelBuilder.AddTransactionalOutboxEntities()` maps - because Identity had never published a message
(research D6). They hold `SellerRegisteredEvent` and `SellerRenamedEvent` until the delivery service sends
them, which is what lets a seller register with RabbitMQ down.

### `Seller` role

Seeded beside `Admin` and `Customer` by `DataInitializer`. A seller account holds **both** `Seller`
and `Customer`: somebody who sells is also somebody who buys.

## Catalog

### `sellers` (new) — a read model, not the truth

| Column | Type | Null | Notes |
| :-- | :-- | :-- | :-- |
| `SellerId` | `uuid` | no | PK. The Identity user id; **no foreign key**, it is another database |
| `ShopName` | `varchar(100)` | no | |
| `ObservedAt` | `timestamptz` | no | When Identity last said so — the guard against an overtaken rename |

Exact shape (from the migration): PK `PK_sellers` on `SellerId` (`ValueGeneratedNever` - the id comes
from Identity); `ShopName character varying(100) NOT NULL`; `ObservedAt timestamp with time zone NOT NULL`.
No other index.

Fed by `SellerRegisteredEvent` and `SellerRenamedEvent`. Nothing in Catalog writes it from a request.
The write is `SellerRepository.TryRecordAsync`: a guarded
`UPDATE sellers SET "ShopName" = @name, "ObservedAt" = @at WHERE "SellerId" = @id AND "ObservedAt" < @at`,
then, if no row exists at all, an insert. A redelivery or an older rename matches nothing; two first
deliveries racing to insert are stopped by the primary key.
It is the same shape as `Product.Availability`, which is fed by Inventory, and carries the same
warning: **it is seconds behind by design and nothing may make a decision on it.** Showing a shop
name is not a decision.

### `products.SellerId` `uuid`, nullable

Null means **the shop itself** (research D4): every product that existed before this feature, and
anything an administrator lists. Not a foreign key, for the same reason as above.

*Corrected on 2026-09-27:* this said "Indexed, because 'my listings' filters on it". The migration adds
the column **without an index** (`AddColumn<Guid>("SellerId", "products", nullable: true)` and nothing
else), and the model snapshot has none. "My listings" filters on it unindexed; at 14 products that costs
nothing measurable, and nothing was measured.

## Resolution at read time

```text
sellerName(product) = sellers[product.SellerId]?.ShopName
                   ?? <the shop's own name, from the storefront's strings>

mayWrite(user, product) = user.IsAdmin
                       || (product.SellerId is not null && product.SellerId == user.Id)
```

A seller id Catalog has not heard of yet reads as the shop itself — the same as a null — so the
eventually-consistent gap looks exactly like the ordinary case (research D1).

## What is deliberately not modelled

- **Seller status.** No pending, approved, suspended. A seller exists or does not.
- **Anything about money owed to a seller.** No payouts, no commission; this system moves no money at
  all.
- **Which seller's stock an order drew on.** An order is one shipment from one warehouse; splitting it
  is a different feature and pretending in the data would be a lie.

## Response shapes (added 2026-09-27)

- `ProductResponse` gains `SellerId` (`Guid?`) and `SellerName` (`string?`), filled from one
  `GetNamesAsync` lookup per page.
- Identity's `SellerProfileResponse(Guid SellerId, string ShopName, DateTime CreatedAt)` for
  `/api/sellers/me` and the rename.
- `ICurrentUser` (Ecommerce.Shared) gains `bool IsInRole(string role)`, read from the token; every test
  double in Cart, Order, Identity and Catalog fixtures implements it.
