# Implementation Plan: Admin revenue less returns

> Written on 2026-09-27, after the feature merged (#176), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Branch**: `084-admin-revenue-returns` | **Date**: 2026-09-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/084-admin-revenue-returns/spec.md`

## Summary

Make the administrator's three Order insights agree with the seller's page about a returned parcel. In
`OrderInsights`, a new `ReturnedIn(from, to)` yields each received return of a sold order in the period with its
`RefundAmount`, its order's payment date, currency and buyer. Revenue per day subtracts it grouped by (shop day,
currency); top buyers subtract it grouped by (buyer, currency); top products drop the lines whose seller's parcel
of that order has a received return. The order count is untouched. One file of production code, one test.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: EF Core with Npgsql (LINQ translated to SQL, including `AT TIME ZONE` from specs/082)

**Storage**: PostgreSQL 16, `ecommerce_order_db` (5434): `orders`, `order_items`, `order_shipments`,
`parcel_returns`, all read only; no schema change

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Order.Tests/SellerInsightsTests`); Bruno against the
rebuilt Order container

**Target Platform**: Order service (5059): `GET /api/orders/insights/{revenue,top-products,top-buyers}`

**Project Type**: A query change inside one service's Infrastructure layer

**Performance Goals**: None stated. Revenue and top buyers each run one extra grouped query

**Constraints**: Response shapes unchanged; money never added across currencies; one definition of a sale
(`OrderInsights.Sold`)

**Scale/Scope**: Three repository methods

## Constitution Check

*GATE: evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. Re-checked after design.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Orders and returns are both Order's own tables; nothing is read from Payment, which records the actual refund - Order's `RefundAmount` is the amount it announced in `ParcelReturnedEvent` |
| **II. Clean Architecture Layering** | **Pass.** The change is inside Infrastructure's `OrderInsights`, behind the Application-declared `IOrderInsights`, whose signatures did not change |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Read-only; no write, no message |
| **IV. Identity Comes From the Token** | **Pass.** Unchanged `Admin` endpoints; no identity involved in the calculation |
| **V. Evidence Over Assumption** | **Pass.** The new test runs the real SQL against PostgreSQL, was red first by exactly the 22,000 refund, and each of the three subtractions removed in turn turned it red; Bruno ran against the rebuilt container |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/084-admin-revenue-returns/
├── spec.md
├── plan.md              # This file
├── research.md          # Four decisions
├── data-model.md        # What is read, and the rules; no schema change
├── quickstart.md
├── contracts/
│   └── http-api.md      # Same shapes, new meaning of three numbers
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderInsights.cs
    # ReturnedIn, ReturnedParcel; RevenueByDayAsync, ProductSalesAsync, BuyersAsync
server/tests/Ecommerce.Order.Tests/SellerInsightsTests.cs
    # The_admin_overview_leaves_a_received_return_out_the_way_the_sellers_page_does; ReturnAsync(refund:), BuyerOfAsync
```

Docs in the same change: `CLAUDE.md`, `docs/features/admin-insights.md`, `docs/features/returns.md`,
`docs/features/seller-insights.md`, `docs/project/decisions.md` (row 66), `docs/overview/project-overview.md`,
`docs/testing/testing-strategy.md`, `docs/project/timeline.md`, `docs/project/backlog.md`.

**Structure Decision**: the test sits in `SellerInsightsTests`, not `InsightsTests`, because its point is that the
admin's figures and the seller's figures agree, and that file already builds orders with several sellers' parcels
and returns.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

Revenue is net, with no separate "refunded" figure: the page does not say how much came back in a period.
