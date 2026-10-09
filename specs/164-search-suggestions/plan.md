# Implementation Plan: Search suggestions while typing

**Branch**: `feature/376-search-suggestions` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md) | **Issue**: #376

## Summary

One anonymous, cached read that answers a few products (through the listing's own search) and a few categories
(matched in memory, folded like the search), and a combobox in the top bar that asks it while the shopper types.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / React 19
**Primary Dependencies**: MediatR, FluentValidation; TanStack Query
**Storage**: none new
**Testing**: `SearchSuggestionTests` (PostgreSQL); Vitest; Bruno; a browser check
**Constraints**: quick (indexed, small answer), anonymous and cacheable; keyboard and screen-reader use
**Scale/Scope**: one query and handler, a controller action; a service method, a hook, a combobox component

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog's own data. |
| **II. Clean Architecture Layering** | **Pass.** The query in Application over the repositories; thin controller. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass - not applicable.** A read. |
| **IV. Identity Comes From the Token** | **Pass.** Anonymous; no user in the request. |
| **V. Evidence Over Assumption** | **Pass.** Diacritics, limits and exclusions tested on PostgreSQL; the keyboard in a browser. |
| **Schema compatibility** | **Pass.** No migration. |

**Post-design re-check**: unchanged - see `tasks.md`.

## Project Structure

```text
specs/164-search-suggestions/
server/src/Services/Catalog/
  Ecommerce.Catalog.Application/Products/Queries/SuggestProducts/SuggestProductsQuery.cs
  Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs
server/tests/Ecommerce.Catalog.Tests/SearchSuggestionTests.cs
client/packages/core: services/product, hooks/product, locales
client/apps/storefront: components/layout/search-box, components/layout/top-bar
bruno/product/
```

## Complexity Tracking

None.
