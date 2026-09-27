# Implementation Plan: Filter the catalogue by price and by what is in stock

**Branch**: `109-catalogue-filters` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #216

## Summary

`GET /api/products` gains `minPrice`, `maxPrice` and `inStock`:

- In the default currency, the bounds filter `products.Price`, which gets a new index.
- In another currency, they filter the product ids of a grouped join on `variant_prices`, which gets a
  `(Currency, Amount)` index.
- `inStock` filters `products.Availability`.

The catalogue page gains a price range and an "In stock only" switch, kept in the URL.

## Technical Context

- **Catalog**:
  - `GetProductsQuery` gains three fields, plus a validator.
  - `IProductRepository.GetPaginatedAsync` gains a `ProductFilter` argument.
  - `ProductRepository` gets the price and stock filters, with a migration for the two indexes.
- **Storefront**:
  - `ProductQuery` gets `minPrice`, `maxPrice` and `inStock`.
  - `pages/catalog` gets the filter controls and the URL state.
  - Words in en/vi.

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: EF Core 10 + Npgsql; TanStack Query

**Storage**: 2 indexes in `ecommerce_catalog_db`

**Testing**: xUnit against real PostgreSQL, with a plan check as in specs/074; Vitest; Bruno

**Target Platform**: Catalog (5057), the storefront

**Performance Goals**: Index-served filtering, with no per-product subplan.

**Constraints**: Never convert between currencies (specs/022). Filter on the same "from" price the card shows.

**Scale/Scope**: 3 parameters, 2 indexes, 1 page

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog answers from its own tables and read model. No call to Inventory; availability is the existing read model, used for display. |
| **II. Clean Architecture Layering** | **Pass.** The query and validator are in Application, the SQL shape in Infrastructure. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** The feature only reads. |
| **IV. Identity Comes From the Token** | **Not applicable.** The listing is anonymous, and no identity is involved. |
| **V. Evidence Over Assumption** | **Pass.** Filter tests against PostgreSQL and plan tests for both currencies - which changed the design (a partial index) - each killed by a mutation; Bruno 348/348. Recorded in `tasks.md`. |

**Post-design re-check** (after implementation): still a pass. Catalog alone (I), SQL in the repository (II), reads only (III), anonymous (IV), evidence in `tasks.md` (V).

## Project Structure

### Documentation (this feature)

```text
specs/109-catalogue-filters/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/http-api.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Catalog/
├── Ecommerce.Catalog.Application/Products/Queries/GetProducts/
├── Ecommerce.Catalog.Application/Common/Interfaces/IProductRepository.cs
├── Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs
├── Ecommerce.Catalog.Infrastructure/Configurations/ProductConfiguration.cs, VariantPriceConfiguration.cs
└── Ecommerce.Catalog.Infrastructure/Migrations/*_AddCatalogueFilterIndexes.cs
server/tests/Ecommerce.Catalog.Tests/CatalogueFilterTests.cs   (new)
client/src/pages/catalog, services/product
bruno/catalog/
```

## Complexity Tracking

No violation to justify.
