# Feature Specification: Revenue counts on the day an order was paid

> Completed on 2026-09-27, after the feature merged (#156), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature Branch**: `072-revenue-paid-day` | **Created**: 2026-09-26 | **Issue**: #116 (closes it)

**Status**: Merged (#156, 2026-09-26).

## Why

The admin Overview (specs/047) and a seller's Insights (specs/068) date revenue by `orders.CreatedAt`. An order
placed at 23:59 and paid at 00:01 counts on the day before it was paid. So does an order the saga settles late
(a slow Payment, or one the timeout sweeper nearly took). An order stores no payment time at all.

## User Scenarios

### US1 - A sale counts on the day it was paid (P1)

An administrator or a seller reading revenue for a day sees the orders paid that day, not the orders placed that day.

**Why this priority**: It is the whole fix: revenue on the wrong day is a wrong report.

**Independent Test**: Place an order on day D, settle it with a completion time on D+1, and ask both the admin's and
the seller's revenue for D and for D+1 (`RevenueDayTests`).

**Acceptance Scenarios**:

1. **Given** an order placed on day D and paid on day D+1, **When** revenue is asked for a period holding only D,
   **Then** the order is absent; **when** for a period holding D+1, **Then** it is in the totals and on D+1's day -
   for the admin and for the seller.
2. **Given** an order from before this change (no `PaidAt`), **When** revenue is asked, **Then** it counts on the day it
   was placed.
3. **Given** an order settled to `Paid`, **When** the completion is delivered again later, **Then** `PaidAt` does not
   move; **given** a failed order, **Then** `PaidAt` stays null.

### US2 - The order says when it was paid (P3)

**Why this priority**: A by-product - the column exists, so the detail shows it.

**Independent Test**: `GET /api/orders/{id}` on a paid order returns `paidAt`.

**Acceptance Scenarios**:

1. **Given** a paid order, **When** its detail is read, **Then** `paidAt` is the settlement's time; null before payment
   and for a failure.

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

### Edge Cases

- **The period and the day disagree.** The filter and the grouping must use the same date, or an order is in a
  period's total but on no day of its chart - the defect #125 was. The expression is written in three places and held
  together by `RevenueDayTests`.
- **A test that moved only `CreatedAt`** to put an order on another day now has to move `PaidAt` too (one voucher test
  changed for that reason).

### Key Entities

- **Order**: gains `PaidAt`, when the saga settled it to `Paid`.

## Success Criteria

- **SC-001**: An order paid a day after it was placed counts on the day it was paid, for the admin and the seller - 3
  `RevenueDayTests`; 6 of 6 mutations caught.
- **SC-002**: Live orders carry a `PaidAt` shortly after `CreatedAt` (0.3 to 1.3 s in the PR's `verify-saga.sh` run);
  the order placed before the rebuild has none.
- **SC-003**: Order tests 263/263.

## Assumptions

- `OrderCompletedEvent.CompletedAt` is the moment of payment: the saga completes on Payment's approval.
- Days are UTC days, as the period rule says (specs/055) - at this merge.

## Out of scope

- A backfill of `PaidAt` for older orders.
- An index for the date filter (#113 was the index work).
- Taking a returned parcel's refund off revenue on the admin Overview (a later change, specs/084).
