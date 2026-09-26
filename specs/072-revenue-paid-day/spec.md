# Feature Specification: Revenue counts on the day an order was paid

**Feature Branch**: `072-revenue-paid-day` | **Created**: 2026-09-26 | **Issue**: #116 (closes it)

## Why

The admin Overview (specs/047) and a seller's Insights (specs/068) date revenue by `orders.CreatedAt`. An order
placed at 23:59 and paid at 00:01 counts on the day before it was paid. So does an order the saga settles late
(a slow Payment, or one the timeout sweeper nearly took). An order stores no payment time at all.

## Requirements

- **FR-001** `orders.PaidAt` is a nullable column, written by the one statement that settles an order to
  `Paid`: the saga's completion, at the time the saga reports. A failed settlement leaves it null.
- **FR-002** Every insight dates a sale by `PaidAt ?? CreatedAt`, for both the period filter and the day it is
  grouped into: the admin's revenue, top products and top buyers, and the seller's revenue and top products.
  Orders from before this have no `PaidAt` and keep counting on the day they were placed. There is no
  backfill, because nothing recorded when they were paid.
- **FR-003** The order detail exposes `paidAt`.

## Acceptance

- An order placed on day D and paid on day D+1 counts on D+1, in the period that holds D+1 and not in the one
  that holds only D. This holds for the admin and for the seller.
- An order with no `PaidAt` counts on the day it was placed.
- Settling twice writes `PaidAt` once, because the guarded statement moves nothing the second time.
