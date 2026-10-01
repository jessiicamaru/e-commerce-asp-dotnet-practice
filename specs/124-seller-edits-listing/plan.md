# Implementation Plan: A seller edits what they listed

**Branch**: `feat/240-seller-edits-listing` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #240

## Summary

Catalog gains `UpdateProductDetailsCommand` behind `PUT /api/products/{id}` and `translatedLanguages` on the lookup; the storefront's seller product page gains three cards over the new and the existing endpoints.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript, React 19  
**Primary Dependencies**: EF Core, MediatR, FluentValidation; TanStack Query  
**Storage**: Catalog's PostgreSQL - no migration  
**Testing**: xUnit against PostgreSQL; Vitest; Bruno  
**Target Platform**: Catalog, the storefront  
**Project Type**: microservice + web client  
**Performance Goals**: n/a  
**Constraints**: additive response field; a new endpoint  
**Scale/Scope**: one command, one field, three cards  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog's own product rows. |
| **II. Clean Architecture Layering** | **Pass.** Command and validator in Application; the cards in `components/seller`, composed by the page. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The change, its audit entry and the review move commit in one save, as every product edit does. |
| **IV. Identity Comes From the Token** | **Pass.** The writer from the token; ownership by `SellerOwnership`. |
| **V. Evidence Over Assumption** | **Planned.** Catalog tests against PostgreSQL, Vitest, Bruno, mutations; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/124-seller-edits-listing/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Commands/UpdateProductDetails/
server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Common/ProductResponse.cs
server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs
server/tests/Ecommerce.Catalog.Tests/ProductDetailsTests.cs
client/src/components/seller/{product-details,product-translations,add-variant}/
client/src/pages/shop-product, pages/shop-product-new, services/product, hooks/product
bruno/seller/
```

## Complexity Tracking

No violation to justify.
