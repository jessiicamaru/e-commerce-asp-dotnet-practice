# Implementation Plan: Orders are recognisable

**Branch**: `feat/248-recognisable-orders` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md) | **Issue**: #248

## Summary

Order's four summary queries project up to three line previews; the storefront adds `orderReference`, `OrderStatusChip` and `OrderRow`, used by four lists, and the reference/status on the two order pages.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript, React 19  
**Primary Dependencies**: EF Core; TanStack Query  
**Storage**: Order's PostgreSQL - no migration  
**Testing**: xUnit against PostgreSQL; Vitest; Bruno  
**Target Platform**: Order, the storefront  
**Project Type**: microservice + web client  
**Performance Goals**: one correlated subquery per row, three rows at most  
**Constraints**: additive response fields  
**Scale/Scope**: four queries, three components, six pages  

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Names as frozen on Order's own lines; pictures from Catalog's public lookup, as the cart does. |
| **II. Clean Architecture Layering** | **Pass.** Projection in the repository; drawing in shared components. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** Reads only. |
| **IV. Identity Comes From the Token** | **Pass.** A seller's previews are filtered by the token's subject, like every figure on a sale (specs/034). |
| **V. Evidence Over Assumption** | **Planned.** Order tests against PostgreSQL, Vitest, Bruno, mutations; recorded in `tasks.md`. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/132-recognisable-orders/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/{OrderResponses,SaleResponses}.cs
server/src/Services/Order/Ecommerce.Order.Application/Orders/Queries/GetOrdersForStaff
server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderRepository.cs
server/tests/Ecommerce.Order.Tests/OrderPreviewTests.cs
client/src/components/order/{order-row,order-status-chip}
client/src/utils/order
client/src/pages/{orders,order,shop-sales,admin-orders,admin-order-search,admin-order}
bruno/order/
```

## Complexity Tracking

No violation to justify.
