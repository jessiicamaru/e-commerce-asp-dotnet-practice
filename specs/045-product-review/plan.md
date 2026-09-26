# Implementation Plan: Product review before sale

> Completed on 2026-09-27, after the feature merged (#97), from the code at that merge, the pull request and docs/features/catalog.md and docs/features/moderation-and-staff.md.

**Branch**: `045-product-review` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/045-product-review/spec.md`

## Summary

Give every product a review state in Catalog and make everything public and everything sellable ask
it. A seller's new product starts `Pending`; staff approve it, reject it with a reason or take an
approved one down with a reason; its seller resubmits a rejected one; and a seller editing the words or
photographs of an approved product sends it back. Each move is one guarded `UPDATE` whose audit entry
and notice commit in the same transaction. The storefront gains a review queue, a moderator's
dashboard (with one new Activity endpoint for "my decisions") and the seller's badge and banner.

The approach is to hide at the source: `Product.IsListed` is read by the listing, the lookup and both
gRPC pricing paths, so checkout, carts and the storefront all follow without a new check anywhere
outside Catalog (research D2).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript with React 19 in `client/`

**Primary Dependencies**: EF Core with Npgsql (`ExecuteUpdateAsync`, execution strategy), MediatR 12.4.1,
FluentValidation 12.1.1, MassTransit 8.3.6 (transactional outbox, already configured in Catalog),
`Ecommerce.Shared` (`IAuditTrail`, `INotifier`, `StaffRoles`); client: TanStack Query, react-i18next,
shadcn/ui

**Storage**: PostgreSQL 16, `ecommerce_catalog_db` on 5433 - five columns and one index on `products`.
Activity's `audit_entries` is read, not changed

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Catalog.Tests`, `ProductReviewTests`); MassTransit
test harness for the published audit entries and notices; Bruno through the gateway; Vitest for the
client

**Target Platform**: Catalog (5057 REST, 6057 gRPC), Activity (5063), storefront, all through the
gateway on 5000. No gateway route was added: `/api/products/{**catch-all}` and
`/api/audit/{**catch-all}` already cover the new addresses

**Constraints**: A rolled-back Catalog image must still read and write `products` (constitution,
Schema evolution). A decision must happen once under concurrency (Principle III). Hiding must cover
checkout, not only pages

**Scale/Scope**: One review state per product; a queue read by a handful of staff. No performance
target was set and none was measured

**Catalog: data**
- `products` gains five columns: `ReviewStatus` (text: `Approved`, `Pending` or `Rejected`),
  `ReviewReason`, `SubmittedAt`, `ReviewedAt` and `ReviewedBy`, with an index on
  (`ReviewStatus`, `SubmittedAt`).
- The migration sets the column default to `'Approved'`. Existing rows are therefore approved, and an
  older image that inserts a product without knowing the column still writes a readable row.

> Precision added on 2026-09-27: "text" above means stored as a string (`HasConversion<string>()`),
> not an integer; the PostgreSQL type is `character varying(20)`, and `ReviewReason` is
> `character varying(500)`. See [data-model.md](data-model.md).

**Catalog: who sees and sells what**
- `Product.IsListed` means `Approved`.
- `ProductVariant.Sellable` requires it, and both pricing paths use it. Checkout therefore refuses an
  unapproved product through the path that already refuses inactive ones.
- `GetPaginatedAsync(listedOnly = true)` filters public listings and search. A seller's own list passes
  `false`.
- `GetProductById` returns null (404) unless the product is listed or the caller is its seller or staff.

**Catalog: review decisions**
- `CreateProduct` starts a seller's product as `Pending`. An administrator's product starts `Approved`.
- `ProductReviewHandlers` covers the queue, approve, reject, take-down and resubmit.
- `IProductRepository.TryReviewAsync` runs a guarded `UPDATE ... WHERE "ReviewStatus" IN (...)`, then a
  stage that writes the audit entry and the notification, all in one transaction.
- Endpoints: `GET /api/products/review` and `POST {id}/approve | reject | take-down` for Staff;
  `POST {id}/resubmit` for Seller or Admin.

> Detail added on 2026-09-27: resubmit's stage writes the audit entry only (the seller is the one
> acting, so there is nobody to tell), and a decision on the shop's own product (`SellerId` null)
> notifies nobody. `TryReviewAsync` runs inside `CreateExecutionStrategy().ExecuteAsync` and clears the
> change tracker first.

**Catalog: edits that send a product back**
- `ProductReview.AfterSellerEditAsync` runs before the one save in six handlers:
  - setting or removing a product translation (the name and description);
  - uploading or removing the product's image;
  - uploading or removing a variant's image.

> Later: specs/056 (#126) added two more callers - translating a variant option and adding a variant -
> making eight.

**Activity**
- `GET /api/audit/mine` (Staff) returns the caller's Moderation entries.

**Shared**
- `NotificationKind.ProductApproved`, `ProductRejected` and `ProductTakenDown`.

**Client**
- `services/moderation` and `hooks/moderation`.
- `/admin/products` is the review queue, with tabs per status and a reason dialog.
- `/admin/moderation` is the dashboard. A moderator's console opens on it.
- Sellers see a `ReviewBadge` in their list and a `ReviewBanner` on the product page (the reason, and
  "send back for review").
- The notification wording covers the three new kinds.

## Research

- **D1 - The status is text with a database default, not a new enum a rolled-back image cannot parse.**
  An older image ignores the column. Pending products would then show during a rollback; that is
  accepted and recorded.
- **D2 - Hide at the source.** Filtering listings, the lookup and pricing in Catalog covers the
  storefront, carts and checkout together. A client-side filter would miss checkout.
- **D3 - Resubmitting after an edit is automatic; after a rejection it is explicit.** An edit to an
  approved product is a change a moderator has not seen. A rejected product is one the seller is still
  working on, so they say when it is ready.
- **D4 - Variant photographs count as images.** The user said "images", and a shopper sees both.

The full set, with alternatives, is in [research.md](research.md) (D1-D10).

## Constitution Check

*GATE: evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0.*

| Principle | Verdict |
| :-- | :-- |
| I | Catalog owns products and their review. Activity serves the audit read. |
| III | Every decision commits with its audit entry and notification. The guarded update makes a second decision a no-op. |
| IV | The reviewer comes from the token. Resubmit goes through `SellerOwnership`. |
| V | 7 integration tests, 3 mutation checks, Bruno for the round trip, and client tests for the queue, dashboard and banner. |

In full, against all five principles:

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The review state is a fact about a product and lives in Catalog's own `products` table; the decision to sell is taken where the product lives, and Order and Cart learn it only through the `sellable` answer they already asked for. Activity answers "my decisions" from its own `audit_entries`, fed by messages Catalog already publishes. No service reads another's database, and no new shared code beyond three notification-kind constants in `Ecommerce.Shared` |
| **II. Clean Architecture Layering** | **Pass.** Domain gains `ProductReviewStatus` and `IsListed` with no dependencies; the rules (`ProductReview`) and the handlers sit in Application and depend on `IProductRepository`, `IAuditTrail` and `INotifier` abstractions; the guarded SQL is in Infrastructure's `ProductRepository`; controllers only `Mediator.Send`. One deviation from the per-use-case folders: the four review commands, the queue query, their validators and one handler class share `Products/Review/ProductReviewFeatures.cs` - a layout choice, not a dependency leak |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Each decision is one transaction: guarded `UPDATE`, then the audit entry and notice staged through the outbox, then one `SaveChangesAsync`, then commit. The `WHERE "ReviewStatus" IN (...)` is the database-enforced guard: a repeated or concurrent attempt affects zero rows, stages nothing and answers 409. The edit hook stages its status change and audit entry before the handler's one save. No new consumer; Activity's existing consumers are idempotent on the entry and notification ids |
| **IV. Identity Comes From the Token** | **Pass.** The reviewer (`ReviewedBy`) and the audit actor come from `ICurrentUser`; no request carries a user id. Deciding is `[Authorize(Roles = StaffRoles.Staff)]`, resubmit is `Seller,Admin` plus `SellerOwnership` (somebody else's is 404), and `/api/audit/mine` filters on the token's id. Who sees a hidden product is `ProductReview.MaySee`, read from the token's roles and id |
| **V. Evidence Over Assumption** | **Pass.** 7 `ProductReviewTests` against a real PostgreSQL (the guarded update and the transaction are the database's), with the harness asserting the published audit entries and notices; 3 mutation checks each turned a named test red; Bruno ran the whole path through the gateway with real tokens (168 requests, 268 tests); `verify-saga.sh` passed. Unverified at merge and said so here: the take-down endpoint had no Bruno request (the request titled "a moderator takes it down instead" calls `/reject` on a pending product; a real take-down request came with #169), and no test raced two approvals concurrently - the 409 test is sequential |

**Post-design re-check**: no violations. The Complexity Tracking table below is empty. The one
recorded cost - a rolled-back image shows pending products (D1) - is a consequence of choosing the
additive schema the constitution asks for, not a departure from it.

## Project Structure

### Documentation (this feature)

```text
specs/045-product-review/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # D1-D10 with rejected alternatives
├── data-model.md        # products columns, index, migration, state transitions
├── quickstart.md        # Validation scenarios
├── contracts/
│   ├── http-api.md      # Review endpoints, audit/mine, changed product responses
│   ├── messages.md      # Audit entries and notices published (no new contract)
│   └── grpc.md          # catalog_pricing.proto unchanged; what `sellable` now means
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task list, all done
```

### Source Code (repository root)

```text
server/src/Services/Catalog/
├── Ecommerce.Catalog.Domain/Entities/
│   ├── Product.cs                         # ReviewStatus, ReviewReason, SubmittedAt, ReviewedAt, ReviewedBy, IsListed; ProductReviewStatus enum
│   └── ProductVariant.cs                  # Sellable requires Product.IsListed
├── Ecommerce.Catalog.Application/
│   ├── Common/ProductReview.cs            # StartsPending, MaySee, AfterSellerEditAsync
│   ├── Common/Interfaces/IProductRepository.cs   # listedOnly, GetForReviewAsync, TryReviewAsync
│   ├── Products/Review/ProductReviewFeatures.cs  # queue query, approve/reject/take-down/resubmit, validators, handlers
│   ├── Products/Commands/CreateProduct/CreateProductCommandHandler.cs
│   ├── Products/Common/ProductResponse.cs        # ReviewStatus, ReviewReason
│   ├── Products/Queries/GetProductById/GetProductByIdQueryHandler.cs
│   ├── Products/Queries/GetMyProducts/GetMyProductsQuery.cs
│   ├── Products/Translations/SetProductTranslationCommand.cs      # set + remove call the edit hook
│   ├── Products/Images/UploadProductImage/UploadProductImageCommand.cs
│   ├── Products/Images/RemoveProductImage/RemoveProductImageCommand.cs
│   └── Products/Images/VariantImageCommands.cs                    # upload + remove call the edit hook
├── Ecommerce.Catalog.Infrastructure/
│   ├── Configurations/ProductConfiguration.cs
│   ├── Migrations/20260923210256_AddProductReview.cs (+ Designer, snapshot)
│   └── Persistence/Repositories/ProductRepository.cs
└── Ecommerce.Catalog.WebApi/
    ├── Controllers/ProductsController.cs  # review, approve, reject, take-down, resubmit
    ├── Grpc/CatalogPricingService.cs      # GetPrices, DescribeProducts: IsActive && IsListed
    └── Program.cs                         # AddNotifier()

server/src/Services/Activity/Ecommerce.Activity.WebApi/Controllers/MyDecisionsController.cs
server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs   # three NotificationKind constants
server/tests/Ecommerce.Catalog.Tests/ProductReviewTests.cs, CatalogTestFixture.cs

client/src/
├── services/moderation/index.ts, services/product/{index,types}.ts
├── hooks/moderation/index.ts, hooks/product/index.ts (useResubmitProduct)
├── pages/admin-products/, pages/admin-moderation/ (each with index.test.tsx)
├── pages/admin-home/, layouts/admin-layout/, routes/index.tsx
├── pages/shop-products/, pages/shop-product/
├── components/product/review-badge/, components/seller/review-banner/ (with test)
└── locales/{en,vi}/{admin,seller,notifications}.json

bruno/seller/ (10 new requests), bruno/security-checks/ (2 new)
CLAUDE.md
```

**Structure Decision**: Everything that decides lives in Catalog, beside the product. The review
handlers share one file rather than five folders because they share one private move (`MoveAsync`),
one response builder and one constructor. Activity gains a controller and no Application code: the
existing `GetAuditEntriesQuery` already filtered by category and actor id.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| - | - | - |

## What this feature does not finish

- **A rollback shows pending products** (D1), and an older image inserting a seller's product writes it
  `Approved` through the column default, i.e. on sale without review. Accepted and recorded.
- **The product's photograph stayed public.** `GET /api/products/{id}/image` and the variant image
  address did not ask `MaySee`, so a hidden product's image could still be fetched by anybody who had
  its address. Closed by specs/081 (#166), which also covered the reviews and questions added later.
- **Two edits that change what a shopper reads did not send a product back**: translating a variant
  option and adding a variant. Closed by specs/056 (#126).
- **The edit hook is a tracked-entity write, not a guarded statement.** It changes `ReviewStatus` on the
  loaded row and saves with the edit; unlike the decisions, it does not re-check the state in the
  `UPDATE`. Whether a seller's edit racing a moderator's decision needs guarding is not recorded.
- **Take-down was not exercised by Bruno at merge** (see Principle V above); the unit test covers it.
- **Documentation**: the pull request updated `CLAUDE.md`; the pages under `docs/features/` that
  describe this feature were written later, in the documentation set (`f466a63`).
