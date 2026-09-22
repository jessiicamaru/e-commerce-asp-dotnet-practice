# Data Model: The Shop Is a Marketplace

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

Fed by `SellerRegisteredEvent` and `SellerRenamedEvent`. Nothing in Catalog writes it from a request.
It is the same shape as `Product.Availability`, which is fed by Inventory, and carries the same
warning: **it is seconds behind by design and nothing may make a decision on it.** Showing a shop
name is not a decision.

### `products.SellerId` `uuid`, nullable

Null means **the shop itself** (research D4): every product that existed before this feature, and
anything an administrator lists. Not a foreign key, for the same reason as above.

Indexed, because "my listings" filters on it.

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
