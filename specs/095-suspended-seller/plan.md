# Implementation Plan: A banned seller's shop is closed

**Branch**: `095-suspended-seller` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #193

## Summary

Identity announces `SellerSuspensionChangedEvent` when it bans a seller or lifts the ban, in the same transaction. Catalog
records it on its `sellers` read model, guarded on the decision's time, and copies it onto the seller's products
(`products.SellerSuspended`); `Product.OnShelf` gains `!SellerSuspended`, so listing, lookup, images, reviews, questions,
views, saving and both pricing paths follow at once. Reinstating tells the savers of products back on sale. Approving a
banned applicant's shop is refused. The users page says a banned seller's shop is closed.

## Technical Context

- Contracts: `server/src/BuildingBlocks/Ecommerce.Contracts/Identity/SellerSuspensionChangedEvent.cs` (new)
- Identity: `Application/Users/UserAdministration.cs` (ban, lift), `Application/ShopApplications/ShopApplicationFeatures.cs` (approve)
- Catalog: `Domain/Entities/Product.cs`, `Seller.cs`; `Infrastructure/Configurations/ProductConfiguration.cs`,
  `SellerConfiguration.cs`; `Persistence/Repositories/SellerRepository.cs`, `ProductRepository.cs`;
  migration `SellerSuspension`; `Application/Sellers/RecordSellerSuspensionCommand.cs` (new);
  `WebApi/Consumers/SellerConsumers.cs`, `Program.cs`
- Storefront: `client/src/pages/admin-users/index.tsx`, `locales/{en,vi}/admin.json`
- Tests: `Ecommerce.Catalog.Tests/SellerSuspensionTests.cs` (new), `Ecommerce.Identity.Tests/ModerationTests.cs`,
  `ShopApplicationTests.cs`, `client/src/pages/admin-users/index.test.tsx`; Bruno `seller/` seq 18-22

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: MassTransit 8.3.6 (EF outbox), EF Core 10 + Npgsql, MediatR 12.4.1

**Storage**: Catalog `ecommerce_catalog_db` (5433) - two columns on `sellers`, one on `products`

**Testing**: xUnit against real PostgreSQL with MassTransit's harness; Vitest; Bruno through the gateway

**Target Platform**: Identity (5056), Catalog (5057)

**Performance Goals**: one `UPDATE products ... WHERE "SellerId" = @id` per ban or lift - `SellerId` is on every product
row the listing already filters; no index needed at this scale (a seller's products are hundreds, not millions)

**Constraints**: the announcement commits with the ban; ordering-safe; expand-only migration; nothing an earlier image
cannot read

**Scale/Scope**: one message, one consumer, one command, one migration, one page note

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity owns the ban and says so; Catalog keeps its own copy and decides its own shelf. No synchronous call is added; the read model is chosen knowingly over specs/031's live call (research D2). |
| **II. Clean Architecture Layering** | **Pass.** The decision is in each Application layer (ban handler, suspension command); statements in Infrastructure; the message in Contracts, where it has both a publisher and a consumer. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Identity stages the message before the ban's one save. Catalog's upsert and product update are one transaction (joining a consumer's), guarded on the decision's time, so redelivery and reordering change nothing; reinstatement notices go through the same outbox. |
| **IV. Identity Comes From the Token** | **Pass.** The ban's caller is from `ICurrentUser` as before; Catalog acts on a system message, not a request. |
| **V. Evidence Over Assumption** | **Pass.** Identity's two tests were red before the fix; Catalog's suite 226/226, Identity's 188/188; four mutations each caught; Bruno ban-and-lift through the gateway against rebuilt containers. |

**Post-design re-check**: no violations. Complexity Tracking records the read model that decides.

## Project Structure

### Documentation (this feature)

```text
specs/095-suspended-seller/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D5
├── data-model.md        # The migration, the upsert, the states
├── quickstart.md
├── contracts/
│   └── messages.md      # SellerSuspensionChangedEvent; the approval's 409
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As listed in Technical Context, plus `CLAUDE.md`, `docs/features/marketplace.md`, `docs/features/moderation-and-staff.md`,
`docs/project/backlog.md`, `docs/project/timeline.md`, and `python docs/tools/generate_reference.py` (a message and
columns were added).

## Complexity Tracking

| Choice | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| A read model that **decides** (the `Seller` entity's own doc said nothing may decide on it) | A ban must close the shop for every read and sale without making each depend on Identity | Asking Identity live (specs/031's answer) would make every listing and checkout fail with Identity down; the cost of lag here is a few seconds' more sales, which staff can cancel (research D2) |

## What this feature does not finish

- A moderator closing a shop without banning the person.
- Staff taking over a suspended seller's paid parcels (they can cancel them, specs/039).
- Bans from before this feature are not announced (research D4).
