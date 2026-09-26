---
description: "Task list for Category Translations"
---

# Tasks: Category Translations

> Written on 2026-09-27, after the feature merged (#63), from the code at that merge, the pull request and docs/features/catalog.md.

**Input**: Design documents from `/specs/026-category-translations/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included, against a real PostgreSQL - the unique index and the cascade are database guarantees.

Reconstructed from the merge diff; every task below is in #63.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task serves (US1-US4)

---

## Phase 1: Foundational - the table

- [X] T001 [P] Create `CategoryTranslation` in `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/CategoryTranslation.cs` and add `Translations` to `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/Category.cs`
- [X] T002 Add `CategoryTranslationConfiguration` (table `category_translations`, lengths 10/100/500, unique `(CategoryId, Language)`, cascade, `ValueGeneratedNever`) to `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/TranslationConfigurations.cs`
- [X] T003 Add `DbSet<CategoryTranslation> CategoryTranslations` to `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/CatalogDbContext.cs`
- [X] T004 Generate migration `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/20260922171155_AddCategoryTranslations.cs` (with its Designer and the updated snapshot)
- [X] T005 Include translations in `GetByIdAsync` and `GetAllAsync` in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/CategoryRepository.cs`

---

## Phase 2: User Stories 1 and 2 - Reading in a language, with fallback (P1) 🎯 MVP

- [X] T006 [P] [US1] Write `CategoryTranslationTests` in `server/tests/Ecommerce.Catalog.Tests/CategoryTranslationTests.cs`: translated reads and says so; untranslated says the default; per-field fallback; replace; remove; unsupported language; 404
- [X] T007 [US1] Add `Language` and `CategoryResponse.From(category, language, defaultLanguage)` with the per-field fallback to `server/src/Services/Catalog/Ecommerce.Catalog.Application/Categories/Common/CategoryResponse.cs`
- [X] T008 [US1] Read `IRequestLanguage` and `LanguageOptions` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Categories/Queries/GetCategories/GetCategoriesQueryHandler.cs` and map through `CategoryResponse.From`

---

## Phase 3: User Story 3 - Writing and removing a translation (P2)

- [X] T009 [US3] Implement `SetCategoryTranslationCommand` (upsert) and `RemoveCategoryTranslationCommand` (no-op when absent) with validators using `MustBeSupported` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Categories/Translations/SetCategoryTranslationCommand.cs`
- [X] T010 [US3] Add `PUT` and `DELETE {id:guid}/translations/{language}`, `[Authorize(Roles = "Admin")]`, and `CategoryTranslationRequest` to `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/CategoriesController.cs`
- [X] T011 [P] [US3] Add Bruno `bruno/category/translate category.yml` and `bruno/category/category falls back to its own text.yml`

---

## Phase 4: User Story 4 - The seeded shop in both languages (P3)

- [X] T012 [P] [US4] Add `en` name and description to each category in `server/seed/cameras.json`
- [X] T013 [US4] PUT the English category names in `server/seed/seed-catalogue.py`, new or existing categories alike

---

## Phase 5: Polish and verification

- [X] T014 [P] Record the rule and the test count (93) in `CLAUDE.md`
- [X] T015 Fix the test that compared against a bare category name while its helper made names unique (the test was wrong, not the code)
- [X] T016 Run every test project (262 pass) and Bruno (87/87 requests, 130 tests); read the categories back from the running stack in both languages
- [X] T017 Write this design record retrospectively under `specs/026-category-translations/` (2026-09-27)
- [X] T018 Merged as [#63](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/63) (`ce12776`) on 2026-09-22

---

## Dependencies & Execution Order

- T001-T005 before everything else; T004 needs T002.
- US1/US2 (T006-T008) and US3 (T009-T011) share only the response mapping (T007).
- US4 needs US3's endpoint.

## Notes

- 18 tasks: 5 foundational, 3 for US1/US2, 3 for US3, 2 for US4, 5 polish.
- 1 test task (T006) holding 7 tests.
