# Implementation Plan: A Storefront That Looks Like a Shop

> Written on 2026-09-27, after the feature merged (#62), from the code at that merge, the pull request and docs/architecture/storefront.md.

**Branch**: `025-storefront-redesign` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/025-storefront-redesign/spec.md`

## Summary

A visual redesign of the React storefront, theme tokens first because they change the most for the
least: warm paper instead of clinical white, a lime accent that carries dark text, and a 1rem radius.
Then a floating top bar with search and a cart count, a bento hero on the landing view, bigger whole-link
product cards with a "Sold by" line, an aperture fallback tinted from the product id, a product page whose
picture sits on its own panel, and variants as clickable cards.

Looking at the result - headless Chrome driven against the dev server, screenshots read - found 95
categories of which 93 were test debris, so the feature also adds `DELETE /api/categories/{id}` (Admin,
409 while products are filed under it) and teaches `seed/clean-test-debris.py` to remove unused
categories. Both seed scripts now force UTF-8 output, because a Windows console could not print the
catalogue's Vietnamese names.

## Technical Context

**Language/Version**: TypeScript with React 19 (client); C# 13 / .NET 10.0 (the Catalog endpoint);
Python 3 (seed scripts)

**Primary Dependencies**: Vite, Tailwind CSS v4, shadcn/ui on Base UI, react-router, TanStack Query,
react-i18next; MediatR 12.4.1 and EF Core with Npgsql on the server

**Storage**: No schema change. Category deletion removes rows from `categories` in
`ecommerce_catalog_db` (5433)

**Testing**: `DeleteCategoryTests` (xUnit, real PostgreSQL). The client had **no unit tests** at this
merge - no `test` script in `client/package.json`; CI's `client` job ran `npm run lint` and
`npm run build` (type-check and build). The visual work was verified by reading screenshots

**Target Platform**: Modern browsers through the Vite dev server on :5173, proxying `/api` to the gateway

**Project Type**: Web client plus one backend endpoint

**Performance Goals**: None stated

**Constraints**: Nothing in the hero may be untrue; accessible contrast on the accent; the radio inside a
variant card must stay for keyboards and screen readers; the search must write the address the catalogue
already reads (`?q=`)

**Scale/Scope**: 13 client files, one Catalog command, one test class, two seed scripts

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The client talks only to the gateway. Category deletion is Catalog deleting its own rows; the cleaner uses the API, not SQL. No message or shared contract is added: no other service holds category rows |
| **II. Clean Architecture Layering** | **Pass.** `DeleteCategoryCommand` lives under `Categories/Commands/DeleteCategory/` and depends on `ICategoryRepository` and `IProductRepository` (new `Remove` and `CountInCategoryAsync`); EF stays in Infrastructure; the controller only dispatches. The client follows its own layering (client/README.md): the hero is a component composed by the catalogue page |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** One `SaveChangesAsync` and no event, so nothing to keep atomic with. The count-then-delete is not a guarded statement, but the `RESTRICT` foreign key from `products.CategoryId` refuses a delete that races a product being filed there; the check exists to give a sentence instead of a constraint error |
| **IV. Identity Comes From the Token** | **Pass.** `[Authorize(Roles = "Admin")]`; the command carries only the category id. The client's top bar asks for a cart only when signed in, and roles remain for drawing only |
| **V. Evidence Over Assumption** | **Pass, and the point of the feature.** It is the first time the storefront was looked at, and looking found four defects. It also produced a false alarm, which was investigated with a probe (`VIEW=500 SCROLL=500`), and the two "fixes" and comments asserting a non-existent bug were reverted. The PR states the limit plainly: verified down to 500px only. `DeleteCategoryTests` runs against a real PostgreSQL |

**Post-design re-check**: no violations. The one limit recorded is evidentiary, not a violation: the
visual result has no automated check at this merge.

## Project Structure

### Documentation (this feature)

```text
specs/025-storefront-redesign/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Seven decisions with rejected alternatives
├── data-model.md        # No schema change
├── quickstart.md        # Validation scenarios
├── checklists/
│   └── requirements.md  # Spec quality checklist
├── contracts/
│   └── http-api.md      # DELETE /api/categories/{id}, and the endpoints the storefront relies on
└── tasks.md             # Reconstructed task list, all done
```

### Source Code (repository root, as changed by #62)

```text
client/src/
├── index.css                                        # theme tokens (light and .dark)
├── layouts/main-layout/index.tsx                    # tinted page, max-w-6xl
├── components/layout/top-bar/index.tsx              # floating bar, search, cart count
├── components/catalog/catalog-hero/index.tsx        # new: bento hero
├── components/product/product-card/index.tsx        # whole-card link, "Sold by", out-of-stock badge
├── components/product/product-image/index.tsx       # aperture fallback tinted from the id
├── components/product/variant-chooser/index.tsx     # variants as cards
├── pages/catalog/index.tsx                          # hero on the landing view only
├── pages/product/index.tsx                          # picture on a panel, "Sold by"
└── locales/{en,vi}/{catalog,common}.json            # hero, soldBy, theShop, searchPlaceholder

server/src/Services/Catalog/
├── Ecommerce.Catalog.Application/
│   ├── Categories/Commands/DeleteCategory/DeleteCategoryCommand.cs   # new
│   └── Common/Interfaces/{ICategoryRepository,IProductRepository}.cs # + Remove, + CountInCategoryAsync
├── Ecommerce.Catalog.Infrastructure/Persistence/Repositories/{Category,Product}Repository.cs
└── Ecommerce.Catalog.WebApi/Controllers/CategoriesController.cs      # + DELETE {id:guid}, Admin

server/tests/Ecommerce.Catalog.Tests/DeleteCategoryTests.cs            # new, 4 tests
server/seed/clean-test-debris.py                                       # + unused categories, UTF-8 output
server/seed/seed-catalogue.py                                          # UTF-8 output
CLAUDE.md                                                              # test count
```

**Structure Decision**: The client's folder-per-thing convention; the hero is a new component under
`components/catalog/`. The category command mirrors `DeleteProductCommand` from specs/024. No gateway
change: `/api/categories` is already routed to Catalog. No Bruno request was added for category deletion.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- Category names are still untranslated, and after the redesign it is the most visible flaw on the page.
  Next PR (specs/026).
- No product photographs; the owner offered to supply assets (specs/030).
- "Sold by The shop" is hard-coded, ready for sellers (specs/027). A code comment in `product-card` names
  sellers "specs/026"; they became specs/027.
- Dark-mode tokens are written but nothing toggles them.
- Verified down to 500px only - wider than a phone.
- No automated check of any of the client changes; the client's unit tests begin with specs/028, and a
  browser-driven check with specs/080.
