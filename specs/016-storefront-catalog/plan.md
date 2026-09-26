# Implementation Plan: Browse, Search and Open a Product

> Written on 2026-09-27, after the feature merged (#46), from the code at that merge, the pull request
> and docs/features/catalog.md and docs/architecture/storefront.md.

**Branch**: `016-storefront-catalog` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/016-storefront-catalog/spec.md`

## Summary

Two pages over Catalog's existing anonymous endpoints. `CatalogPage` (`/`) reads `q`, `category`,
`sort` and `page` from the URL, asks `GET /api/products` for 12 at a time, lists them as cards and
pages through them; `ProductPage` (`/products/:id`) reads one product and tells a 404 apart from a
failure. `src/api/catalog.ts` holds the typed calls and a `money` formatter. No backend change.

## Technical Context

**Language/Version**: TypeScript ~6.0 / React 19

**Primary Dependencies**: `react-router-dom` (`useSearchParams`, `useParams`); the call layer from
specs/014

**Storage**: None. The URL is the page's state.

**Testing**: No client tests (none until specs/028). The calls the pages make were checked with curl
through the Vite proxy against the containerised stack; lint and build in CI.

**Target Platform**: Browser, via Vite on `:5173`, gateway on `:5000`

**Project Type**: Web front end only

**Performance Goals**: None. One listing request per change of URL, one category request per visit.

**Constraints**: Anonymous calls only; no stock count shown; the price is net of tax.

**Scale/Scope**: 2 pages, 1 API module, 2 routes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0, after the merge.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The pages show Catalog's availability read model as a label and decide nothing from it - which is what the principle's "one owner per fact" rule asks of a display copy (stock is Inventory's). They reach Catalog only through the gateway |
| **II. Clean Architecture Layering** | **Pass - not applicable.** No service code changed |
| **III. Atomic Writes and Idempotent Messaging** | **Pass - not applicable.** Read-only |
| **IV. Identity Comes From the Token** | **Pass.** Every call is sent `anonymous: true` against endpoints Catalog declares `[AllowAnonymous]`; no identity is sent or needed |
| **V. Evidence Over Assumption** | **Pass, with the gap named.** Each call the pages depend on was exercised through the proxy (sort, paging, search, categories, an unknown id, the SPA route), and the output recorded. The pages were not clicked through in a real browser, and the pull request says so |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/016-storefront-catalog/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Four decisions
├── data-model.md        # No table; the client types and the URL parameters
├── quickstart.md        # The proxy checks and a browser walk-through
├── contracts/
│   └── http-api.md      # The three Catalog endpoints relied on; none changed
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task list, all done
```

### Source Code (repository root)

```text
client/src/
├── api/catalog.ts           # listProducts, getProduct, listCategories, money; Product, Category, Page, SortBy
├── pages/CatalogPage.tsx    # the listing, the filters, the pager, <Availability>
├── pages/ProductPage.tsx    # one product, the 404 message, the tax note
├── App.tsx                  # routes / and /products/:id
└── index.css                # grid, cards, placeholder tile
```

**Structure Decision**: follows specs/014 - one file per page, one module per backend area under
`src/api/`. `Availability` is exported from `CatalogPage` and reused by `ProductPage` so the two labels
cannot drift.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- **Images** (#45, later specs/019).
- **Inactive products** are not filtered (recorded, not filed - see the spec).
- **No add-to-cart** yet (specs/017).
- **Not clicked through in a real browser** at this merge.
