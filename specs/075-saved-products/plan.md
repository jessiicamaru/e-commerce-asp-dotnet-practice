# Implementation Plan: A shopper saves a product for later

> Completed on 2026-09-27, after the feature merged (#159), from the code at that merge, the pull request and docs/features/saved-products.md.

**Branch**: `075-saved-products` | **Date**: 2026-09-26 | **Spec**: [spec.md](spec.md) | **Issue**: #109

**Input**: Feature specification from `/specs/075-saved-products/spec.md`

## Summary

A signed-in shopper saves a product for later with a heart on a product card or on the product page, reads what
they saved at `/saved`, and is told when a saved product comes back in stock.

Catalog gains one table, `saved_products`, keyed `(CustomerId, ProductId)` with a cascade from `products`, and four
endpoints about the caller's own list. Saving is one `INSERT ... ON CONFLICT DO NOTHING`. The list reads each
product through the listing's own response, now, in the request's language and currency. The back-in-stock notice
rides on the availability rollup that already existed: `RecomputeProductRollupAsync` now reports whether its own
`UPDATE` turned the product from unavailable to available, and only then does
`RecordStockAvailabilityCommandHandler` tell each saver of a listed product, through the consumer's outbox. No
message contract, gateway route or other service changed.

The design below is the one written when the feature was built, kept as it was; its research section is carried
into [research.md](./research.md) as D1 to D3, with five further decisions (D4 to D8) the record makes without
naming them.

## Design (Catalog)

- The `SavedProduct` entity and its configuration, plus the migration `AddSavedProducts`.
- `ISavedProductRepository`:
  - `SaveAsync` (`ON CONFLICT DO NOTHING`);
  - `UnsaveAsync` (a guarded delete);
  - `GetPageAsync(customerId, page, size)`, which returns products with their translations and variant prices, the
    same includes as the listing, plus `SavedAt`;
  - `IdsAsync(customerId)`;
  - `SaverIdsAsync(productId)`.
- `SavedProductFeatures.cs` holds the commands, the queries and one handler class.
  - Saving checks `Product.IsListed` and `IsActive`. Anything else is a 404, as the public lookup is.
  - The page maps each product with the listing's own response (the language and the currency of the request),
    wrapped in `SavedProductResponse(Product, SavedAt, Available)`. `Available` means listed, active and in stock.
- **Back in stock.** `RecomputeProductRollupAsync` returns whether the product's `Availability` went from false
  to true.
  - That is one statement: a CTE reads the old value under `FOR UPDATE`, then the UPDATE runs.

    > Corrected on 2026-09-27: the code at the merge has no `FOR UPDATE`. The CTE is a plain
    > `SELECT "Availability" AS was FROM products WHERE "Id" = @id`, which reads the snapshot the statement
    > started from, and the `UPDATE` returns it with `RETURNING (SELECT was FROM before) AS "Was",
    > p."Availability" AS "Now"`. The only row lock is the one the `UPDATE` takes. See research D4.
  - `RecordStockAvailabilityCommandHandler` then notifies each saver of a listed product, through the consumer's
    outbox, in the same transaction.
- `SavedProductsController`, at `api/products` with `[Authorize]`: the routes `{id}/saved`, `saved` and
  `saved/ids`. They do not collide with `{id:guid}`, because `saved` is not a guid.

## Storefront

- The `SavedProduct` service, and the hooks `useSavedIds`, `useToggleSaved` and `useSavedProducts`.
- `components/product/save-button` is the heart, with `aria-pressed`, on the product card and on the product page.
- `pages/saved` is routed at `/saved` and linked from the user menu.
- The notification kind `SavedBackInStock`, in `vi` and `en`.

## Research

The full decisions, with their rejected alternatives, are in [research.md](./research.md). The three recorded here
when the feature was built:

- **D1 - Catalog owns it.** A saved product is about products: price, availability and listing status are all
  Catalog's. The cart is another thing (a quantity, meant to be bought).
- **D2 - a hidden product cannot be saved, but one saved before it was hidden stays.** Saving is asking about
  a public product. The list is the shopper's own record, and it says "no longer available" rather than
  silently dropping something they chose.
- **D3 - the notice is on the rollup's flip.** Inventory announces per variant, many times. Only the product
  going from none in stock to some in stock is news.

## Technical Context

**Language/Version**: C# on .NET 10.0 (server); TypeScript with React 19 (storefront)

**Primary Dependencies**: EF Core with Npgsql, MediatR, FluentValidation, MassTransit (Catalog's consumer outbox,
`UseEntityFrameworkOutbox<CatalogDbContext>`), `Ecommerce.Shared` (`ICurrentUser`, `INotifier`,
`IRequestLanguage`, `IRequestCurrency`, `GlobalExceptionHandler`); storefront: axios, TanStack Query, react-router,
react-i18next, shadcn/ui `Button`, `lucide-react`'s `HeartIcon`

**Storage**: PostgreSQL 16, `ecommerce_catalog_db` (host port 5433) - one new table, `saved_products`

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Catalog.Tests`, 7 new tests in `SavedProductTests`,
published messages read from MassTransit's test harness); Vitest with jsdom and Testing Library (3 new test files);
Bruno (6 new requests in `bruno/product/`, seq 61-66)

**Target Platform**: Catalog on 5057 behind the gateway on 5000; the storefront through Vite's `/api` proxy or the
nginx image

**Project Type**: Existing backend microservice (Clean Architecture) plus the storefront

**Performance Goals**: None stated. A page of saved products is one count and one paged read with split-query
includes, plus one batched shop-name lookup - the listing's cost, not a call per item

**Constraints**: The shopper's id comes only from the token (Constitution IV). Saving must be idempotent under
concurrency by a database mechanism (Constitution III). A product not on sale must be indistinguishable from one
that does not exist (specs/045). The notice must commit with the availability it announces, and must fire at most
once per saver per flip

**Scale/Scope**: One table, four endpoints, one repository method's return type, one notification kind; one
component, one page, one hook module, one service class in the storefront

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. This section was written
after the merge (2026-09-27); the plan as first written had no Constitution Check. The assessment is of the design
as built.

| Principle | Assessment |
| :-- | :-- |
| **I. Service Autonomy** | **Pass.** The table is in Catalog's own database and names only Catalog's own `products` (research D1). The notice reaches Activity as a message (`UserNotificationRequested`), never as a write to its database. The availability it reacts to is Catalog's read model of Inventory's stock and is used only to *tell* somebody, never to sell - checkout still reserves against Inventory's row, so "one owner per fact" is untouched |
| **II. Clean Architecture Layering** | **Pass.** `SavedProduct` in Domain; `ISavedProductRepository`, the commands, queries and handlers in Application (`Products/Saved/`), which depends on `INotifier` and `ICurrentUser` abstractions only; the raw SQL in Infrastructure's repositories; the controller in WebApi only dispatches through MediatR. One deviation in form, not in direction: the four requests and their handler share one file, `SavedProductFeatures.cs`, rather than one folder per use case - the shape `ReviewFeatures.cs`, `ProductReviewFeatures.cs` and `ProductViewFeatures.cs` already had in Catalog |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Saving is idempotent by the primary key and `ON CONFLICT DO NOTHING`, not by a check in code; unsaving is a guarded delete that affects zero rows on a repeat. The notices are published through the consumer's outbox inside the consumer's transaction, so they commit with the availability change or not at all. A repeated announcement cannot notify twice: the variant's time guard affects zero rows, and the flip is read inside the one `UPDATE` (research D4) |
| **IV. Identity Comes From the Token** | **Pass.** Every endpoint is `[Authorize]`; each handler reads the caller from `ICurrentUser.Id`; no command, query or route carries a shopper id, and the storefront's service test asserts the URLs name none |
| **V. Evidence Over Assumption** | **Pass.** The idempotency guarantee is the database's, and it is tested against a real PostgreSQL with twenty concurrent saves. Four mutation checks each turned a test red (PR #159). A live check through the gateway saw a real `SavedBackInStock` notice after an administrator stocked a saved product. What was not verified is stated in [quickstart.md](./quickstart.md) |

**Post-Phase 1 re-check**: no violations. The Complexity Tracking table below is empty because nothing needed
justifying: the feature adds one table and reuses the listing's response, the existing rollup statement, the
existing notifier and the existing gateway route.

## Project Structure

### Documentation (this feature)

```text
specs/075-saved-products/
├── spec.md                  # Feature specification
├── plan.md                  # This file
├── research.md              # D1-D8, with rejected alternatives
├── data-model.md            # saved_products, AddSavedProducts, the rollup's new return value
├── quickstart.md            # Eight validation scenarios
├── contracts/
│   ├── http-api.md          # The four endpoints
│   └── messages.md          # StockAvailabilityChangedEvent (consumed), UserNotificationRequested SavedBackInStock
├── checklists/
│   └── requirements.md      # Spec quality checklist
└── tasks.md                 # Task list, all done
```

### Source Code (repository root)

```text
server/src/Services/Catalog/
├── Ecommerce.Catalog.Domain/Entities/SavedProduct.cs                                   # new
├── Ecommerce.Catalog.Application/
│   ├── Common/Interfaces/ISavedProductRepository.cs                                    # new
│   ├── Common/Interfaces/IProductRepository.cs                                         # RecomputeProductRollupAsync -> Task<bool>
│   ├── Products/Saved/SavedProductFeatures.cs                                          # new: commands, queries, validator, handlers
│   └── Products/Availability/RecordStockAvailabilityCommandHandler.cs                  # tells the savers on the flip
├── Ecommerce.Catalog.Infrastructure/
│   ├── Configurations/SavedProductConfiguration.cs                                     # new
│   ├── Persistence/CatalogDbContext.cs                                                 # DbSet<SavedProduct>
│   ├── Persistence/Repositories/SavedProductRepository.cs                              # new
│   ├── Persistence/Repositories/ProductRepository.cs                                   # the CTE and RETURNING
│   ├── DependencyInjection.cs                                                          # registers the repository
│   └── Migrations/20260926095302_AddSavedProducts.cs (+ .Designer.cs, model snapshot)  # new
└── Ecommerce.Catalog.WebApi/Controllers/SavedProductsController.cs                     # new

server/src/BuildingBlocks/Ecommerce.Shared/Notifications/
├── Notifier.cs                                                                         # NotificationKind.SavedBackInStock
└── notification-kinds.json                                                             # "SavedBackInStock": { "required": ["product"] }

server/tests/Ecommerce.Catalog.Tests/
├── SavedProductTests.cs                                                                # new, 7 tests
└── CatalogTestFixture.cs                                                               # registers the repository

client/src/
├── services/saved-product/{index.ts, types.ts, index.test.ts}                          # new
├── hooks/saved-product/index.ts                                                        # new
├── components/product/save-button/{index.tsx, index.test.tsx}                          # new
├── components/product/product-card/index.tsx                                           # the heart beside the link
├── components/layout/user-menu/index.tsx                                               # "Saved"
├── pages/saved/{index.tsx, index.test.tsx}                                             # new
├── pages/product/index.tsx                                                             # the heart beside the title
├── routes/index.tsx                                                                    # /saved behind RequireAuth
├── constants/query-keys/index.ts                                                       # savedIds, savedProducts
├── locales/{vi,en}/{catalog,common,notifications}.json                                 # saved.*, nav.saved, kind.SavedBackInStock
└── test/render.tsx                                                                     # renderSignedOut

bruno/product/                                                                          # 6 new requests, seq 61-66
```

Documentation touched in the same pull request: `docs/features/saved-products.md` (new), `docs/README.md`,
`docs/overview/project-overview.md`, `docs/project/backlog.md`, `docs/project/timeline.md`,
`docs/reference/api.md`, `docs/reference/data-model.md` (regenerated), `docs/testing/testing-strategy.md`, and a
paragraph in `CLAUDE.md`.

**Structure Decision**: Everything server-side lives in Catalog, beside the products it names (research D1). The
storefront follows `client/README.md`: a folder per thing with an `index` entry, a service class over axios with its
types in `types.ts` (the class `SavedProduct` and the model `SavedItem` cannot share a name), the TanStack Query
layer in `hooks/`, and the page composing the existing `ProductCard` and `Pager`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| - | - | - |

## What this feature does not finish

- **Only Inventory's announcements report the flip.** `SetVariantPriceCommand`, `AddProductVariantCommand` and
  `UpdateProductVariantCommand` also call `RecomputeProductRollupAsync` and discard the returned value, so a
  product that becomes available because a variant was added or reactivated tells nobody. Nor does a product that
  is approved or restored to the shelf while already in stock - the flip is about availability, not listing. Not
  recorded whether this was considered.
- **No "price dropped" notice**, and no sharing a list (spec, Out of scope).
- **No digest**: a shopper with many saved products that come back gets one notice per product.
- **The notice names the product in its default language**, because Catalog does not know the saver's language.
  Since specs/083 (#171) an email goes with the notice, written by Identity in the reader's language, with the same
  default-language product name.
