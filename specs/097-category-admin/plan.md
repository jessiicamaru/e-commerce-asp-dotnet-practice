# Implementation Plan: Administrators manage categories

**Branch**: `097-category-admin` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #195

## Summary

`PUT /api/categories/{id}` (Admin) renames a category's own, default-language text; the slug never changes. Creating a
category whose slug is taken is a 409 instead of a 500. `/admin/categories` lists every category with its Vietnamese and
English names, creates one with a slug suggested from its name, edits both languages (Vietnamese through the new endpoint,
English through specs/026's translation endpoints) and deletes, showing the server's refusals in its words.

## Technical Context

- Catalog: `Application/Categories/Commands/UpdateCategory/UpdateCategoryCommand.cs` (new: command, validator, handler);
  `CreateCategoryCommandHandler.cs` (409); `WebApi/Controllers/CategoriesController.cs` (`PUT {id}`)
- Storefront: `services/category` (`listIn`, `create`, `update`, `translate`, `removeTranslation`, `remove`),
  `hooks/category` (`useCategoriesIn`, `useCategoryChanges`), `utils/shared/search.ts` (`slugOf`),
  `pages/admin-categories` (new: page, row), route (Admin), menu, `locales/{en,vi}/admin.json`
- Tests: `Ecommerce.Catalog.Tests/CategoryAdminTests.cs`, `client/src/pages/admin-categories/index.test.tsx`; Bruno `category/` 3, 6, 7

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: MediatR, FluentValidation, `Ecommerce.Shared` (audit, exceptions); TanStack Query

**Storage**: none new - `categories`, `category_translations`

**Testing**: xUnit against real PostgreSQL; Vitest; Bruno

**Target Platform**: Catalog (5057) and the storefront

**Performance Goals**: n/a - a handful of rows

**Constraints**: slug immutable; Admin only for writes

**Scale/Scope**: one endpoint, one fix, one page

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog's own table; the page calls Catalog only. |
| **II. Clean Architecture Layering** | **Pass.** Command, validator and handler in Application; the controller sends. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The rename stages its audit entry before the one save, like every Catalog write. |
| **IV. Identity Comes From the Token** | **Pass.** Admin role on the route; the audit's actor from `ICurrentUser`. |
| **V. Evidence Over Assumption** | **Pass.** Four server tests (the 409 one red before the fix), seven page tests; three mutations each caught; Bruno through the gateway. |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/097-category-admin/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D4
├── data-model.md        # The columns written; IsActive noted
├── quickstart.md
├── contracts/
│   └── http-api.md      # PUT /api/categories/{id}; the create 409
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As in Technical Context, plus `docs/features/catalog.md`, CLAUDE.md, backlog, timeline, and `generate_reference.py`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- `IsActive` stays dead data; nested categories are not edited in the UI.
