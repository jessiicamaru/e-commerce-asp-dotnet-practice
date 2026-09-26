# Implementation Plan: A saved product back on sale by any route tells whoever saved it

**Branch**: `091-back-on-sale-notices` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #182

## Summary

Every Catalog handler that recomputes a product's rollup now tells its savers when that recompute flips it back in
stock, in the same transaction as the change: `IProductRepository.SaveAndRecomputeRollupAsync` replaces the separate
save-then-recompute in `UpdateProductVariant`, `AddProductVariant` and `SetVariantPrice`. A moderator's approval tells
savers when the product is in stock, inside the guarded move's transaction. One helper, `SavedProductNotices`, sends the
notice and email for every route, and the default words become "available again".

## Technical Context

- `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Saved/SavedProductNotices.cs` (new)
- `.../Common/Interfaces/IProductRepository.cs`, `Ecommerce.Catalog.Infrastructure/.../ProductRepository.cs`
- `.../Products/Availability/RecordStockAvailabilityCommandHandler.cs` (uses the helper)
- `.../Products/Variants/UpdateProductVariant/UpdateProductVariantCommand.cs`,
  `.../AddProductVariant/AddProductVariantCommand.cs`, `.../Products/Prices/SetVariantPriceCommand.cs`
- `.../Products/Review/ProductReviewFeatures.cs`
- `server/src/Services/Identity/Ecommerce.Identity.Application/Email/EmailTemplates.cs` (words)
- `client/src/locales/{en,vi}/{notifications,admin}.json` (words)
- `server/tests/Ecommerce.Catalog.Tests/SavedProductTests.cs`

**Language/Version**: C# 13 / .NET 10.0; JSON locale files

**Primary Dependencies**: EF Core 10 + Npgsql (`EnableRetryOnFailure`), MassTransit 8.3.6 (EF outbox), MediatR 12.4.1,
`Ecommerce.Shared` (`INotifier`, `IEmailSender`)

**Storage**: PostgreSQL `ecommerce_catalog_db` (5433) - no schema change

**Testing**: xUnit against a real PostgreSQL with MassTransit's test harness (`Ecommerce.Catalog.Tests`); Vitest for the
storefront; mutation checks

**Target Platform**: Catalog (5057); the storefront and Identity only for words

**Performance Goals**: one extra read of `saved_products` per flip - as on Inventory's route

**Constraints**: notices commit with the change (Principle III); one notice per flip; no new kind

**Scale/Scope**: one helper, one repository method, five handlers touched, four tests, six wording strings

## Design

- `SavedProductNotices.BackOnSaleAsync(product, saved, notifier, email)` - the notice and email to each saver;
  `WhenBackInStock(products, productId, ...)` - the callback an edit passes: reads the product and tells when it is
  listed and active (the flip already says it is in stock).
- `SaveAndRecomputeRollupAsync(productId, whenBackInStock)` - joins `CurrentTransaction` if any, else
  `CreateExecutionStrategy().ExecuteAsync` around `BeginTransactionAsync`: save, recompute, callback-and-save on a flip,
  commit.
- Approval: `if (to == Approved && product.IsActive && product.Availability)` inside the existing `stage` callback.
- `RecordStockAvailabilityCommandHandler` keeps its own flow (it runs in a consumer's transaction already) and calls
  the helper.

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog decides from its own tables and publishes existing messages; Identity words the email from its own template; no new call between services. |
| **II. Clean Architecture Layering** | **Pass.** The rule (who to tell, when) is in the Application layer; the transaction mechanics are in the Infrastructure repository behind an Application interface; controllers unchanged. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass - the reason for the design.** The three edit handlers saved and recomputed in separate commits; notices there are staged before a save inside one transaction with the change and the recompute. Approval's notices are staged in the guarded move's transaction. The flip is decided by one statement (specs/075), so a concurrent route cannot make two notices. |
| **IV. Identity Comes From the Token** | **Pass.** Recipients come from `saved_products`; the moderator and seller identities from `ICurrentUser`, as before; nothing from a request body. |
| **V. Evidence Over Assumption** | **Pass.** The routes were surveyed in code (research D3); two tests failed before the fix and two guard against over-telling; four mutations each caught; the Catalog suite 215/215 and the storefront suite green. |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/091-back-on-sale-notices/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D5
├── data-model.md        # "On sale" and its transitions; no schema change
├── quickstart.md
├── contracts/
│   └── messages.md      # Who publishes SavedBackInStock now; the words before and after
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As listed in Technical Context, plus `CLAUDE.md`, `docs/features/saved-products.md`, `docs/features/email.md`,
`docs/project/backlog.md`, `docs/project/timeline.md`.

No endpoint, message, table or gateway route changes, so `docs/reference/` is not regenerated.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- A price in a new currency does not tell anybody (research D4).
- The product name in the notice is the default language's, as before (specs/083's known limit).
