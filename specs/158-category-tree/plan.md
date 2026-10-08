# Implementation Plan: Categories in a tree - departments and their categories

**Branch**: `feature/363-category-tree` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md) | **Issue**: #363

## Summary

Enforce a two-level tree on the existing `ParentCategoryId` (create, a new move endpoint, delete), widen the listing's
category filter to subcategories, and show the tree in the storefront (filter, hero, breadcrumb) and the back office
(tree, create under, move). The seed declares departments.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / React 19
**Primary Dependencies**: MediatR, FluentValidation, EF Core; TanStack Query, react-i18next
**Storage**: `categories.ParentCategoryId` (exists, `RESTRICT` FK, indexed) - no migration
**Testing**: `CategoryTreeTests` (PostgreSQL); Vitest for the tree util, filter, hero, breadcrumb, back office page;
Bruno; the seed checker
**Constraints**: two levels; the rename endpoint unchanged; no new request on the product page
**Scale/Scope**: one command + rules, two handler changes, one repository filter, ~6 client components, seed, docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Categories are Catalog's; no other service reads them. |
| **II. Clean Architecture Layering** | **Pass.** Rules in Application (`CategoryTree`), the filter in the repository, the endpoint in the controller doing nothing but `Mediator.Send`. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** A move is one row and its audit entry in one save (the audit through the outbox, before the save). No message is added. |
| **IV. Identity Comes From the Token** | **Pass.** Admin-only by role; the actor of the audit entry comes from `ICurrentUser`. |
| **V. Evidence Over Assumption** | **Pass.** Every rule tested against PostgreSQL, the listing's filter included; the seed run on the stack seeded by #360. |

**Post-design re-check**: see `tasks.md`.

## Project Structure

```text
specs/158-category-tree/
server/src/Services/Catalog/Ecommerce.Catalog.Application/Categories/CategoryTree.cs               the rules
server/src/Services/Catalog/Ecommerce.Catalog.Application/Categories/Commands/MoveCategory/        the move
server/src/Services/Catalog/Ecommerce.Catalog.Application/Categories/Commands/{CreateCategory,DeleteCategory}/
server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/ICategoryRepository.cs HasChildren/CountChildren
server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/{Category,Product}Repository.cs
server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/CategoriesController.cs           PUT {id}/parent
server/tests/Ecommerce.Catalog.Tests/CategoryTreeTests.cs
client/packages/core/src/utils/category/                                                            categoryTree
client/packages/core/src/services/category/                                                         move
client/apps/storefront/src/components/catalog/{catalog-filters,catalog-hero}, components/product/category-breadcrumb
client/apps/back-office/src/pages/admin-categories/
client/packages/core/src/locales/{vi,en}/{catalog,admin}.json
server/seed/catalogue.py, seed-catalogue.py, catalogue/*.json                                       departments
bruno/category/                                                                                     move + negatives
docs/features/catalog.md, docs/reference (generated), CLAUDE.md, timeline, backlog
```

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| None | - | - |
