# Implementation Plan: A Catalogue of Real Cameras

> Written on 2026-09-27, after the feature merged (#60), from the code at that merge, the pull request
> and docs/features/catalog.md.

**Branch**: `023-camera-catalogue` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/023-camera-catalogue/spec.md` (reconstructed)

## Summary

A Python seeder, `server/seed/seed-catalogue.py`, reads `server/seed/cameras.json` and builds the
catalogue through the gateway as an administrator: categories, products with their first variant,
further variants, English translations of products and options, dollar prices, and stock in Inventory.
It is idempotent by SKU and never deletes. Writing it surfaced two defects in Catalog, both fixed here:
`VariantOptionResponse` now carries `Id` (the option-translation endpoint from specs/021 was
unreachable without it), and `ProductVariant.Summarise` and `Localized` order options by their stored
name, with a data-only migration, `NormaliseOptionSummaryOrder`, rewriting the summaries already stored.

## Technical Context

**Language/Version**: Python 3 (standard library only: `json`, `urllib`) for the seeder; C# 13 / .NET
10.0 for the Catalog fixes

**Primary Dependencies**: None new. The seeder uses the gateway's public API; the fixes touch Domain,
Application and one EF migration.

**Storage**: `ecommerce_catalog_db` (host port 5433) - no schema change; one data-only migration.
Inventory's `stock_items` receive stock through `PUT /api/stock/{variantId}`.

**Testing**: Two new tests in `Ecommerce.Catalog.Tests/VariantTests` against a real PostgreSQL; the
ordering one verified to go red without the fix. The seeder was run against the stack and its rows
read back in both languages and currencies; run twice for idempotency.

**Target Platform**: A started stack, gateway on `:5000` (`GATEWAY_URL` overrides it)

**Project Type**: A data tool beside the backend, plus a Catalog fix

**Performance Goals**: None. Stock writes retry up to 20 times, 0.5 s apart.

**Constraints**: Every row through the API; idempotent; never deletes; no invented images; prices
labelled approximate.

**Scale/Scope**: 2 categories, 14 products, 23 variants, 195 units of stock.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0, after the merge.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The seeder touches no database: Catalog's data goes to Catalog's API, stock to Inventory's, and Inventory learns each variant from Catalog's own event, as in production. The dollar and dong prices are set separately, as specs/022 requires of the one owner of price |
| **II. Clean Architecture Layering** | **Pass.** The ordering rule is in the Domain (`ProductVariant.Summarise`) and repeated for the translated read in Application (`Localized`), both ordering by the stored name; the id is added to an Application response record. No layer gained a dependency |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** No handler's write order changed. The seeder is idempotent at its own level (by SKU; upserts for translations, prices and stock). The migration is a single `UPDATE` computing each summary from the variant's own option rows, so running it on any state yields the same result |
| **IV. Identity Comes From the Token** | **Pass.** The seeder signs in with `ADMIN_EMAIL` / `ADMIN_PASSWORD` and sends every write with that bearer token to `[Authorize(Roles = "Admin")]` endpoints; nothing is written as an identity it did not prove |
| **V. Evidence Over Assumption** | **Pass.** The seeded rows were read back from the running stack in both languages and currencies - which is how the by-position translation bug was caught ("by reading the rows, not by looking at the screen"). The ordering test was verified red without the fix. The prices are the one thing not verified, and the data says so at the top of the file and in CLAUDE.md |

**Post-design re-check**: no violations. Schema evolution: the migration changes data only, and only
the word order of a string an earlier image already reads, so a redeployed earlier image works
unchanged; `Down` is deliberately empty because "the previous order was whatever the database happened
to return, which is not a state anything can be restored to".

## Project Structure

### Documentation (this feature)

```text
specs/023-camera-catalogue/
├── plan.md              # This file
├── spec.md              # Feature specification (reconstructed)
├── research.md          # Eight decisions
├── data-model.md        # The seed file, the migration, what the seeder writes where
├── quickstart.md        # Seed, reseed, read back, test
├── contracts/
│   └── http-api.md      # VariantOptionResponse.Id (the one API change) and every call the seeder makes
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task list, all done
```

### Source Code (repository root)

```text
server/seed/cameras.json                  # the data, with the price warning in `_about`
server/seed/seed-catalogue.py             # the seeder
server/src/Services/Catalog/
├── Ecommerce.Catalog.Domain/Entities/ProductVariant.cs                 # Summarise orders by name
├── Ecommerce.Catalog.Application/Products/Common/Localized.cs          # the translated summary, same order
├── Ecommerce.Catalog.Application/Products/Common/VariantResponse.cs    # VariantOptionResponse(Id, Name, Value)
└── Ecommerce.Catalog.Infrastructure/Migrations/20260922161858_NormaliseOptionSummaryOrder.cs (+ Designer)
server/tests/Ecommerce.Catalog.Tests/VariantTests.cs                   # two tests
client/src/services/product/types.ts                                  # option `id`
CLAUDE.md                                                             # how to seed; the price warning
```

**Structure Decision**: `server/seed/` is new - a tool run against a started stack, not part of any
service, so it lives beside the solution rather than inside a project. Data and code are separate files
so the catalogue can be edited without touching the script.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- **No images** - products read back with `imageUrl: null`.
- **Category names in Vietnamese only** (specs/026 translated them).
- **The 94 junk products remain** - no delete endpoint (specs/024 added one; specs/073 a cleaner that
  keeps what `cameras.json` names).
- **The prices are unverified** - treat any individual number as wrong until somebody looks it up.
- **Nobody has clicked through the storefront in a browser** (specs/080 added Playwright).
