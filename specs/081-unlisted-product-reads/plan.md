# Implementation Plan: What hangs on a product off the shelf

> Written on 2026-09-27, after the feature merged (#169), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Branch**: `081-unlisted-product-reads` | **Date**: 2026-09-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/081-unlisted-product-reads/spec.md`

## Summary

Close the three reads that ignored a product's listing (#166). Reviews and questions ask the rule the public
lookup already asks, `ProductReview.MaySee`, and answer the same `Product not found.` 404. Images cannot be decided
by the caller's identity, because an `<img>` request carries no token, so each image gets a random
`ImageAccessKey` in its address (`&k=`): on sale any address opens it, off the shelf only the image's own key does,
and the response is then `private, no-cache`. The key is written by the guarded statement that already switches an
image (specs/019) and cleared when the image is removed. One additive migration adds the two columns and gives
every stored image a key. The storefront needed no change.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MediatR 12.4.1, EF Core with Npgsql, `Ecommerce.Shared` (`ICurrentUser`, `StaffRoles`,
`NotFoundException`)

**Storage**: PostgreSQL 16, `ecommerce_catalog_db` on host port 5433; image bytes in the S3 bucket
`product-images` (specs/079), untouched

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Catalog.Tests`, 7 new tests in
`UnlistedProductReadsTests`); Bruno through the gateway; the four Playwright browser flows (#117) in Edge

**Target Platform**: Catalog service (REST on 5057, container 8080), through the gateway on 5000

**Project Type**: A change inside one existing Clean Architecture service

**Performance Goals**: None stated. The review and question reads gain one product lookup by primary key

**Constraints**: Addresses already cached for products on sale must keep working (FR-002); the migration must only
add (constitution, Schema evolution); a 404 must not tell "off the shelf" from "does not exist"

**Scale/Scope**: Every product and variant image row; three read handlers, two image handlers, three image-switching
commands

## Constitution Check

*GATE: evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. Re-checked after design.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Everything is Catalog's own data: the listing, the image rows and the new key. No other service is read or called |
| **II. Clean Architecture Layering** | **Pass.** The rule lives in Application (`ProductImageKey.MayServe`, the handlers); the guarded `UPDATE` in Infrastructure's `ProductRepository`; the controller only reads `k` and sets the cache header from what the query returns |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** No message is published or consumed. The key is written by the image switch's one guarded `UPDATE ... WHERE "ImageUpdatedAt" = @seen`, so the key and the image it opens change together or not at all |
| **IV. Identity Comes From the Token** | **Pass.** Who may read reviews and questions comes from `ICurrentUser` via `MaySee`. The image key is not an identity claim: it is a capability handed out only in responses whose reader passed `MaySee`, and nothing in it names a user |
| **V. Evidence Over Assumption** | **Pass.** The issue's probe was re-run against the rebuilt stack before and after (four requests, 200 to 404); the tests run against a real PostgreSQL; five mutations of the rule each turned the tests red; Bruno and the browser flows ran against the rebuilt Catalog |

**Post-design re-check**: no violations. One point recorded rather than hidden: the key is made with
`Guid.NewGuid()`, not `Guid.CreateVersion7()` as ADR-001 asks for keys. It is not a key; it must be unguessable,
and a version 7 Guid carries a timestamp in its first bits (research D2).

## Project Structure

### Documentation (this feature)

```text
specs/081-unlisted-product-reads/
├── spec.md              # What and why
├── plan.md              # This file
├── research.md          # Five decisions with rejected alternatives
├── data-model.md        # Two added columns and the backfill
├── quickstart.md        # The issue's probe and the seller's view, by hand
├── contracts/
│   └── http-api.md      # The image, review and question reads
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # What was done, in order
```

### Source Code (repository root)

```text
server/src/Services/Catalog/
├── Ecommerce.Catalog.Domain/Entities/
│   ├── Product.cs                       # ImageAccessKey
│   └── ProductVariant.cs                # ImageAccessKey
├── Ecommerce.Catalog.Application/
│   ├── Common/Interfaces/IProductRepository.cs      # TrySetImageAsync / TrySetVariantImageAsync take the key
│   ├── Products/Images/ProductImageKey.cs           # &k= in the address; MayServe
│   ├── Products/Images/GetProductImage/GetProductImageQuery.cs   # Key; ProductImage.Public
│   ├── Products/Images/GetVariantImageQuery.cs      # Key
│   ├── Products/Images/UploadProductImage/UploadProductImageCommand.cs   # a new key per image
│   ├── Products/Images/RemoveProductImage/RemoveProductImageCommand.cs   # key cleared
│   ├── Products/Images/VariantImageCommands.cs      # the same for a variant
│   ├── Reviews/ReviewFeatures.cs                    # GetProductReviewsQuery asks MaySee
│   └── Questions/QuestionFeatures.cs                # GetProductQuestionsQuery asks MaySee
├── Ecommerce.Catalog.Infrastructure/
│   ├── Persistence/Repositories/ProductRepository.cs   # the guarded switch sets the key
│   └── Migrations/20260926150608_AddImageAccessKeys.cs
└── Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs   # ?k=, CacheFor

server/tests/Ecommerce.Catalog.Tests/
├── UnlistedProductReadsTests.cs         # new, 7 tests
└── ProductImageTests.cs                 # the address now ends in &k=

bruno/product/upload image.yml           # expects &k=
bruno/seller/                            # seq 78-81: take-down, reviews 404, questions 404, seller reads
```

Documentation touched in the same change: `CLAUDE.md`, `docs/features/catalog.md`,
`docs/features/ratings-and-reviews.md`, `docs/reference/data-model.md`, `docs/testing/testing-strategy.md`,
`docs/overview/project-overview.md`, `docs/project/timeline.md`, `docs/project/backlog.md`.

**Structure Decision**: No new project or folder. The rule sits next to the code that builds the address
(`ProductImageKey`), so the place that hands the key out and the place that checks it are one file.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- An image address somebody already holds keeps opening that image until the image is replaced (the key is a
  capability, not an expiring signature).
- Writing a review of a product off the shelf was still open; it was closed a day later by
  [specs/085](../085-unlisted-review-writes/) (#174).
