# Implementation Plan: A compare-at price per variant

**Branch**: `feature/369-compare-at-price` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md) | **Issue**: #369

## Summary

Two nullable columns beside the prices they compare against, each with a CHECK that it is above its price (research
D1); a command to set and clear one; the price writers clearing a compare-at they reach; the variant and product
responses carrying it; an `onSale` listing filter. The storefront strikes it through with the percentage off, filters on
it, and the seller sets it beside each price. Checkout is not touched.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / React 19
**Primary Dependencies**: EF Core (migration), MediatR, FluentValidation; TanStack Query
**Storage**: 2 nullable columns + 2 CHECK constraints in `ecommerce_catalog_db`
**Testing**: `CompareAtPriceTests` (PostgreSQL), `CheckoutQuoteTests`-style proof that pricing ignores it; Vitest;
Bruno; a browser check
**Constraints**: expand-only; display only - nothing that charges reads it
**Scale/Scope**: 2 columns, 2 commands, 3 writers adjusted, responses, one filter, 3 client places, seed

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog's own columns; no other service reads them. |
| **II. Clean Architecture Layering** | **Pass.** Properties in Domain, rules in Application, mapping/constraints/migration in Infrastructure, thin controller. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** A compare-at and its audit entry commit in one save; a price change and the clearing of its compare-at are one save; a repeated PUT is the same value. No new message. |
| **IV. Identity Comes From the Token** | **Pass.** `SellerOwnership` from `ICurrentUser`. |
| **V. Evidence Over Assumption** | **Pass.** The CHECK and the clearing tested on PostgreSQL, a mutation, the filter, and a checkout proving the charge unchanged; a browser check. |
| **Schema compatibility** | **Pass.** Two nullable columns; nothing dropped, renamed or narrowed. |

**Post-design re-check**: unchanged - see `tasks.md`.

## Project Structure

```text
specs/161-compare-at-price/

server/src/Services/Catalog/
  Ecommerce.Catalog.Domain/Entities/{ProductVariant,VariantPrice}.cs        (+ CompareAt)
  Ecommerce.Catalog.Application/Products/Prices/CompareAtPriceCommands.cs   (set, clear, the clearing rule)
  Ecommerce.Catalog.Application/Products/Prices/SetVariantPriceCommand.cs  (clears)
  Ecommerce.Catalog.Application/Products/Variants/UpdateProductVariant/... (clears)
  Ecommerce.Catalog.Application/Products/Common/{Priced,VariantResponse,ProductResponse}.cs
  Ecommerce.Catalog.Application/Products/Queries/GetProducts/*              (onSale)
  Ecommerce.Catalog.Infrastructure/Configurations/*  + migration AddCompareAtPrices
  Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs (filter)
  Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs
server/tests/Ecommerce.Catalog.Tests/CompareAtPriceTests.cs
server/seed/  (a few compare-at prices)

client/packages/core: types, service, hooks, a price component, locales
client/apps/storefront: card and product page, the "On sale" filter, the seller's price editor
bruno/product/
```

## Complexity Tracking

None.
