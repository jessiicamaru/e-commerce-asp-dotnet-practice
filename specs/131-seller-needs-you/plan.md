# Implementation Plan: A seller's home says what needs them

**Branch**: `feat/247-seller-needs-you` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md) | **Issue**: #247

## Summary

Order's seller sales query gains a part-status filter; the storefront adds `useSellerWaiting` (four counts, own keys), a Needs you panel on `/shop`, badges in the seller layout, and two corrected labels.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript, React 19  
**Primary Dependencies**: EF Core; TanStack Query  
**Storage**: Order's PostgreSQL - no migration  
**Testing**: xUnit against PostgreSQL; Vitest; Bruno  
**Target Platform**: Order, the storefront  
**Project Type**: microservice + web client  
**Performance Goals**: four small requests per minute for a seller  
**Constraints**: optional query parameter, additive  
**Scale/Scope**: one filter, one hook, one panel, one layout  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each count from the service that owns it (Order, Catalog questions, Order returns, Identity payout account). |
| **II. Clean Architecture Layering** | **Pass.** Filter in the repository, validation in the query's validator; drawing in pages and layouts. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** Reads only. |
| **IV. Identity Comes From the Token** | **Pass.** The seller is the token's subject; the filter adds no id. |
| **V. Evidence Over Assumption** | **Planned.** Order tests against PostgreSQL, Vitest, Bruno, mutations; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/131-seller-needs-you/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Order/Ecommerce.Order.Application/Orders/Queries/GetMySales/GetMySalesQuery.cs
server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderRepository.cs
server/src/Services/Order/Ecommerce.Order.WebApi/Controllers/OrdersController.cs (sales)
server/tests/Ecommerce.Order.Tests/
client/src/hooks/order, layouts/seller-layout, pages/shop, services/order
bruno/seller/
```

## Complexity Tracking

No violation to justify.
