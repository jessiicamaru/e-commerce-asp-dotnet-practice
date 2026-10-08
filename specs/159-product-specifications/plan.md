# Implementation Plan: Product specifications per category

**Branch**: `feature/366-product-specifications` | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md) | **Issue**: #366

## Summary

Five new Catalog tables (expand-only) for specifications declared per category with translated choice options, and a
product's values. Admin endpoints under `/api/categories/{id}/specifications`, one product endpoint replacing the
values (ownership, review, audit), the lookup carrying them, and a listing filter by option. Storefront table and
filters, seller's editor, back office management, seed for every vertical.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / React 19
**Primary Dependencies**: EF Core (migration), MediatR, FluentValidation; TanStack Query
**Storage**: 5 new tables in `ecommerce_catalog_db`; no change to existing ones
**Testing**: `ProductSpecificationTests` (PostgreSQL), `MyDataTests` (inventory), `CatalogueWritesTests` (eviction);
Vitest; Bruno; the seed checker
**Constraints**: expand-only migration; text values untranslated; only choices filterable
**Scale/Scope**: domain + 5 configurations + migration, ~10 commands/queries, controller actions, 4 client screens, seed

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog's own tables; no other service reads them or is told. |
| **II. Clean Architecture Layering** | **Pass.** Entities in Domain, rules and handlers in Application, mapping/migration/queries in Infrastructure, thin controller. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Each write and its audit entry (and a review resubmission) commit in one save; the PUT replaces the whole set, so a repeat is a no-op. No new message. |
| **IV. Identity Comes From the Token** | **Pass.** Ownership through `SellerOwnership` from `ICurrentUser`; staff by role. |
| **V. Evidence Over Assumption** | **Pass.** Rules tested on PostgreSQL, the filter and lookup in two languages, a mutation; the seed run on the stack. |
| **Schema compatibility** | **Pass.** Five new tables, nothing dropped, renamed or narrowed: an older image ignores them. |

**Post-design re-check**: see `tasks.md`.

## Project Structure

```text
specs/159-product-specifications/
server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/Specification*.cs
server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/Specification*Configuration.cs, Migrations/
server/src/Services/Catalog/Ecommerce.Catalog.Application/Specifications/                 commands, queries, responses
server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/...                      lookup + filter
server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/CatalogueWrites.cs  new tables
server/src/Services/Catalog/Ecommerce.Catalog.Application/MyData/MyData.cs                  declared
server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/{Categories,Products}Controller.cs
server/tests/Ecommerce.Catalog.Tests/ProductSpecificationTests.cs
client/packages/core/src/services/{category,product}, hooks
client/apps/storefront: product page table, catalog filters, seller product page editor
client/apps/back-office: categories page specifications panel
server/seed/catalogue.py, seed-catalogue.py, catalogue/*.json
bruno/specifications/
docs/features/catalog.md, docs/reference (generated), docs/architecture/microservices-design.md, CLAUDE.md, timeline, backlog
```

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| None | - | - |
