# Implementation Plan: Category Translations

> Written on 2026-09-27, after the feature merged (#63), from the code at that merge, the pull request and docs/features/catalog.md.

**Branch**: `026-category-translations` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/026-category-translations/spec.md`

## Summary

Add `category_translations` to Catalog - the same shape as `product_translations` (specs/021): an id, the
category id, a language tag, a name and a nullable description, unique on `(CategoryId, Language)`,
cascading from the category. `CategoryResponse.From` applies the per-field fallback and reports the
language the text ended up in. `PUT /api/categories/{id}/translations/{lang}` upserts and
`DELETE /api/categories/{id}/translations/{lang}` removes, both Admin. The category repository now
includes translations in both of its reads. The seeder writes the English names of the two real
categories from `cameras.json`.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; Python 3 for the seeder

**Primary Dependencies**: MediatR 12.4.1, FluentValidation 12.1.1 (`MustBeSupported` from specs/021),
EF Core with Npgsql, `Ecommerce.Shared.Localization` (`IRequestLanguage`, `LanguageOptions`)

**Storage**: PostgreSQL 16, `ecommerce_catalog_db` (5433): one new table, migration
`20260922171155_AddCategoryTranslations`

**Testing**: `CategoryTranslationTests` (xUnit, real PostgreSQL); Bruno `category/translate category` and
`category/category falls back to its own text`

**Target Platform**: Catalog (5057) behind the gateway (5000)

**Project Type**: Backend microservice, Clean Architecture

**Performance Goals**: None stated. `GetAllAsync` now includes translations - two categories in the real
catalogue

**Constraints**: Additive schema only (an earlier image must still run); per-field fallback identical to
products'; `Content-Language` and `Vary: Accept-Language` continue to come from the service-wide
localization set up in specs/021

**Scale/Scope**: Two real categories, two languages

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog owns categories and their text; no other service reads either. No message or shared contract changes |
| **II. Clean Architecture Layering** | **Pass.** `CategoryTranslation` is a plain Domain entity; the commands, validators and handlers sit in Application under `Categories/Translations/` (the folder products use for the same job); the mapping is an `IEntityTypeConfiguration` in Infrastructure; the controller only dispatches |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** One `SaveChangesAsync` per command and no event. The upsert is idempotent by design - a second identical PUT leaves one row with the same text - and the unique `(CategoryId, Language)` index enforces one row per language in the database, not only in code |
| **IV. Identity Comes From the Token** | **Pass.** Both writes are `[Authorize(Roles = "Admin")]`; the reads stay anonymous as before. No command carries a user id |
| **V. Evidence Over Assumption** | **Pass.** The tests run against a real PostgreSQL (the unique index and the cascade are database guarantees). The PR read the categories back from the running stack in both languages. One test failed first time because the test compared against a bare name while its helper made names unique; the PR records that the test was wrong, not the code |

**Post-design re-check**: no violations. The migration only creates a table and an index, so an earlier
Catalog image runs against it unchanged.

## Project Structure

### Documentation (this feature)

```text
specs/026-category-translations/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Five decisions with rejected alternatives
├── data-model.md        # category_translations and its migration
├── quickstart.md        # Validation scenarios
├── checklists/
│   └── requirements.md  # Spec quality checklist
├── contracts/
│   └── http-api.md      # PUT/DELETE translations; the language field on reads
└── tasks.md             # Reconstructed task list, all done
```

### Source Code (repository root, as changed by #63)

```text
server/src/Services/Catalog/
├── Ecommerce.Catalog.Domain/Entities/
│   ├── Category.cs                                  # + Translations
│   └── CategoryTranslation.cs                       # new
├── Ecommerce.Catalog.Application/Categories/
│   ├── Common/CategoryResponse.cs                   # + Language, + From(category, language, default)
│   ├── Queries/GetCategories/GetCategoriesQueryHandler.cs   # reads the request language
│   └── Translations/SetCategoryTranslationCommand.cs        # new: set + remove, validators, handlers
├── Ecommerce.Catalog.Infrastructure/
│   ├── Configurations/TranslationConfigurations.cs  # + CategoryTranslationConfiguration
│   ├── Migrations/20260922171155_AddCategoryTranslations.cs (+ Designer, snapshot)
│   ├── Persistence/CatalogDbContext.cs              # + CategoryTranslations
│   └── Persistence/Repositories/CategoryRepository.cs       # Include(Translations) in both reads
└── Ecommerce.Catalog.WebApi/Controllers/CategoriesController.cs   # + PUT/DELETE translations

server/tests/Ecommerce.Catalog.Tests/CategoryTranslationTests.cs  # new, 7 tests
server/seed/cameras.json                                          # + "en" per category; the rest re-serialised
server/seed/seed-catalogue.py                                     # PUT the English category names
bruno/category/translate category.yml                             # new
bruno/category/category falls back to its own text.yml            # new
CLAUDE.md                                                         # test count, the rule
```

**Structure Decision**: Mirrors product translations in every layer, down to the folder name and the
file holding both the set and the remove command. No gateway change: `/api/categories/{**catch-all}`
already reaches Catalog. No client change: the storefront already shows `category.name` from the
response, and it already sends `Accept-Language`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- There is no screen to translate a category in this feature; administrators use the API.
- Slugs stay in one form.
- The `cameras.json` diff is large (342 lines added, 34 removed) but only the two `en` blocks under
  `categories` are content: the rest is the file re-serialised with one array element per line.
