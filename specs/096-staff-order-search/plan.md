# Implementation Plan: Staff find any order

**Branch**: `096-staff-order-search` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #194

## Summary

A new Admin-only `GET /api/orders/staff` lists any order, newest first, filtered by an id prefix, a customer id and a
status (Paid including Completed). The storefront's `/admin/orders/find` takes an id prefix or an email - an email is
turned into a person through Identity's staff search first - offers a status filter, and labels each row with the
customer's name and email from Identity's lookup.

## Technical Context

- Order: `Application/Orders/Queries/GetOrdersForStaff/GetOrdersForStaffQuery.cs` (new: query, response, validator,
  handler); `IOrderRepository.SearchForStaffAsync` / `OrderRepository`; `OrdersController` (`GET staff`)
- Storefront: `services/admin` (`findOrders`, types), `hooks/admin` (`useStaffOrderSearch`), `constants/query-keys`,
  `pages/admin-order-search` (new), `routes`, `layouts/admin-layout` (menu), `locales/{en,vi}/admin.json`
- Tests: `Ecommerce.Order.Tests/StaffOrderSearchTests.cs`, `client/src/pages/admin-order-search/index.test.tsx`; Bruno `order/` 12-13

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: EF Core 10 + Npgsql (`EF.Functions.Like`, `Guid.ToString()` → `::text`), FluentValidation;
TanStack Query

**Storage**: none new - `orders` in `ecommerce_order_db` (5434)

**Testing**: xUnit against real PostgreSQL; Vitest; Bruno through the gateway

**Target Platform**: Order (5059) and the storefront

**Performance Goals**: a staff screen; a page is one query with correlated counts, like the fulfilment queue

**Constraints**: Admin only; Order never learns emails

**Scale/Scope**: one query, one repository method, one page

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order answers from its own table by ids; the storefront composes Identity's person search and lookup (research D1). No new call between services. |
| **II. Clean Architecture Layering** | **Pass.** Query, validator and handler in Application; the statement in the Infrastructure repository; the controller only sends. |
| **III. Atomic Writes and Idempotent Messaging** | **Not applicable** - a read. |
| **IV. Identity Comes From the Token** | **Pass.** The permission is the Admin role on the token; `customerId` is a filter an administrator chooses, not the caller's identity. |
| **V. Evidence Over Assumption** | **Pass.** Eight server tests and four page tests; three mutations each caught; Bruno 403 for a customer through the gateway. |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/096-staff-order-search/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D4
├── data-model.md        # The columns read; the query
├── quickstart.md
├── contracts/
│   └── http-api.md      # GET /api/orders/staff
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As in Technical Context, plus `docs/features/fulfilment-and-delivery.md`, `docs/project/backlog.md`,
`docs/project/timeline.md`, CLAUDE.md, and `python docs/tools/generate_reference.py` (an endpoint was added).

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- Moderators cannot search orders.
- No index backs the prefix match (research D3).
