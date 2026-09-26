# Implementation Plan: The Shop Is a Marketplace

> Completed on 2026-09-27, after the feature merged (#64), from the code at that merge, the pull request and docs/features/marketplace.md.

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
**Target Platform** (added 2026-09-27): Identity (5056) and Catalog (5057) behind the gateway (5000);
Identity's first RabbitMQ connection
**Performance Goals** (added 2026-09-27): none as a number; FR-003's rule - a page of products costs one
read-model query, never a call to Identity per product
**Project Type** (added 2026-09-27): backend microservices, Clean Architecture, plus two lines of the
storefront

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

**Post-design re-check** (2026-09-27, against the merged code): still no violations, with one design
cost found while building and paid within the principles: Identity had no broker, so D1's event needed
MassTransit, the EF outbox and its tables in Identity (research D6), wired with `UseBusOutbox()` so the
registration and its announcement commit together (Principle III). Principle V caught what unit tests
could not: with the write endpoints still `[Authorize(Roles = "Admin")]`, every handler test passed and a
real seller got 403 on her own product; the attributes became `Seller,Admin` and Bruno now asserts the
exact status.

**Recorded rather than solved**: the shop-name read model is eventually consistent, and for a few
seconds a new seller's products read as the shop's own (research D1). That is the same thing an older
product reads as, so it is invisible rather than wrong.

## Project Structure

*Corrected on 2026-09-27 against the merge:* the migrations are `20260922173126_AddSellerProfilesAndOutbox`
(Identity) and `20260922173905_AddSellers` (Catalog); the rename and "my shop" live in one file,
`Application/Sellers/SellerCommands.cs`; Catalog's read-model write is `Application/Sellers/RecordSellerCommand.cs`
and both consumers are in `WebApi/Consumers/SellerConsumers.cs`; Identity also gained
`WebApi/Controllers/SellersController.cs`. `ICurrentUser` in `Ecommerce.Shared` gained `IsInRole`, and the
gateway gained `sellers-route` and `sellers-root-route` to Identity. **The storefront's sign-up page was not
changed**: #64 touched only the product card, the product page and the product type (the "Sold by" line),
so there was no seller option at sign-up; registering a seller was API-only. The tree below is the plan as
written.

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

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

(From the PR, recorded 2026-09-27.)

- **No seller console.** Managing listings is the API's surface; the storefront gets the shop name
  (research D5). specs/028 built the console.
- **No seller sign-up in the storefront** (see the correction above).
- **No approval, suspension or payouts.** Approval arrived as shop applications in specs/044; payouts in
  specs/037.
- **A seller cannot set stock** - Inventory's stock write stayed Admin only until specs/031.
- **One order is still one shipment from one warehouse** - per-seller shipments are specs/035.
- **The image writes had no cross-seller test** at this merge, although they call the same check.
- Identity was left with an `IdentitySvc` endpoint-name prefix and no consumers, so that the first consumer
  it gains cannot collide with another service's queue.
