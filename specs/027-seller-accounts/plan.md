# Implementation Plan: The Shop Is a Marketplace

**Branch**: `027-seller-accounts` | **Date**: 2026-09-23 | **Spec**: [spec.md](spec.md)

## Summary

Give the shop a second kind of participant. Identity grows sellers and a shop name; Catalog records
which seller a product belongs to, keeps a read model of shop names so a listing costs no extra
calls, and refuses a seller any write to somebody else's listing as though it did not exist.

## ⚠️ A decision taken on the owner's behalf

The owner was asked whether a seller should be usable immediately or wait for an administrator to
approve them, and did not answer before this was built.

**Chosen: usable immediately.** The approval queue is what a real marketplace does and is roughly
twice the work — a state on the seller, an admin screen, a refusal on every write while pending, and
a lifecycle to verify. It is recorded in the spec's assumptions as deliberately not built.

If the answer comes back "approval", the shape here survives it: a status column on
`seller_profiles` and one guard in the ownership helper.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript 6 + React 19
**Primary Dependencies**: EF Core + Npgsql, MediatR, MassTransit, FluentValidation
**Storage**: one new table each in Identity and Catalog, one nullable column on `products`
**Testing**: xUnit against real PostgreSQL (Identity, Catalog); Bruno; the storefront builds
**Constraints**: every existing product keeps selling; an anonymous catalogue read must not need Identity
**Scope**: Identity, Catalog, `Ecommerce.Contracts`, the storefront

## Constitution Check

| Principle | Check | Result |
| :-- | :-- | :-- |
| I. Service autonomy | Identity owns sellers; Catalog owns which product is whose and answers from its own database. The shop name crosses as an event, never as a synchronous call on a read path. | Pass |
| II. Clean Architecture | `SellerProfile` and `Seller` are entities in their services' Domain; ownership is enforced in Application handlers; EF mapping in Infrastructure. | Pass |
| III. Atomic writes, idempotent messaging | Registration stages the profile and publishes through the outbox in one transaction. The Catalog consumer upserts under an `ObservedAt` guard, so a redelivery or an overtaken rename changes nothing. | Pass |
| IV. Identity from the token | The seller id is `ICurrentUser.Id`. **No request body carries a seller id** — that is the defect specs/009 and issue #18 both were. | Pass |
| V. Evidence over assumption | Cross-writes are tested per operation, not once (SC-001). The rename path is tested without touching a product (SC-004). | Pass |
| Schema evolution | Two new tables, one nullable column. No rename, no narrowing, no backfill. | Pass |
| Invariants in the database | `seller_profiles.UserId` is the primary key, so one shop per account is a constraint and not a convention. | Pass |

No violations, so Complexity Tracking is empty.

**Recorded rather than solved**: the shop-name read model is eventually consistent, and for a few
seconds a new seller's products read as the shop's own (research D1). That is the same thing an older
product reads as, so it is invisible rather than wrong.

## Project Structure

```text
server/src/BuildingBlocks/
  Ecommerce.Contracts/Identity/SellerEvents.cs     SellerRegisteredEvent, SellerRenamedEvent

server/src/Services/Identity/
  Domain/Entities/SellerProfile.cs, Constants/RoleNames.cs (+ Seller)
  Application/Auth/Commands/RegisterSeller/        register + shop name, grants Seller and Customer
  Application/Sellers/Commands/RenameShop/
  Infrastructure/Migrations/AddSellerProfiles
  WebApi/Controllers/AuthController                POST /api/auth/register-seller

server/src/Services/Catalog/
  Domain/Entities/Seller.cs                        the read model
  Application/Common/SellerOwnership.cs            the one ownership check every write calls
  Application/Products/...                         each write asks it
  Application/Products/Queries/GetMyProducts/
  Infrastructure/Migrations/AddProductSeller
  WebApi/Consumers/SellerRegisteredConsumer, SellerRenamedConsumer

client/src/
  pages/sign-up (a seller option), product card + product page show the shop name
bruno/                                             registration, ownership refusals, the rename
```

## Order of work

1. Contracts and Identity: the role, the profile, registration, the events.
2. Catalog: the read model and its consumers — a name to show before there is anything to show it on.
3. Catalog: `products.SellerId`, the ownership check, and every write that must call it.
4. Reads: the seller on a product response, and "my listings".
5. The storefront: the name where the hard-coded string is, and a way to register.
6. Evidence: tests per operation, Bruno, the stack, the negative controls.

## Design artifacts

[research.md](research.md) · [data-model.md](data-model.md) · [contracts/api.md](contracts/api.md)
