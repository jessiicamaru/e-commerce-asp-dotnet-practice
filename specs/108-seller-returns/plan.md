# Implementation Plan: A seller's list of the returns of their parcels

**Branch**: `108-seller-returns` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #215

## Summary

A read-only feature:

- `GET /api/orders/sales/returns` lists the caller's own parcels' returns, filtered on `parcel_returns.SellerId`.
- The sales list gains each sale's return state.
- The shop console gains `/shop/returns`, and a badge on `/shop/sales`.

## Technical Context

- **Order**:
  - `GetSaleReturnsQuery` and its validator in `Returns/ReturnFeatures.cs`.
  - `IReturnRepository.GetPageAsync` gains an optional seller filter.
  - The `sales/returns` route in `ReturnsController`.
  - `SaleSummaryResponse.ReturnStatus`, and the subquery in `OrderRepository.GetSalesPageAsync`.
- **Storefront**:
  - `Order.saleReturns`, the `useSaleReturns` hook, `pages/shop-returns`, the route and menu entry, and the badge in
    `pages/shop-sales`.
  - Words in en/vi.

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: EF Core 10 + Npgsql; TanStack Query

**Storage**: none new

**Testing**: xUnit against real PostgreSQL; Vitest; Bruno

**Target Platform**: Order (5059), the storefront

**Performance Goals**: One indexed query per page. The badge adds one scalar subquery per row of a page of at most 50.

**Constraints**: A seller never sees another seller's or the shop's return.

**Scale/Scope**: 1 route, 1 response field, 1 page

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order answers from its own tables. |
| **II. Clean Architecture Layering** | **Pass.** The query is in Application, and the SQL is in Infrastructure. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable.** The feature only reads: no write, no message. |
| **IV. Identity Comes From the Token** | **Pass.** No seller id in the request. The filter is `ICurrentUser.Id`. |
| **V. Evidence Over Assumption** | **Planned.** Order tests for each exclusion, mutations, and Bruno through the gateway. Recorded in `tasks.md`. |

**Post-design re-check**: to be done once implemented; results go in `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/108-seller-returns/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/http-api.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Order/
├── Ecommerce.Order.Application/Returns/ReturnFeatures.cs
├── Ecommerce.Order.Application/Orders/Common/SaleResponses.cs
├── Ecommerce.Order.Infrastructure/Persistence/Repositories/ReturnRepository.cs, OrderRepository.cs
└── Ecommerce.Order.WebApi/Controllers/ReturnsController.cs
server/tests/Ecommerce.Order.Tests/ReturnTests.cs
client/src/pages/shop-returns/ (new), pages/shop-sales
bruno/seller/, bruno/security-checks/
```

## Complexity Tracking

No violation to justify.
