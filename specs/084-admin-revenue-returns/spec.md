# Feature Specification: Admin revenue less returns

> Completed on 2026-09-27, after the feature merged (#176), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature Branch**: `084-admin-revenue-returns` | **Created**: 2026-09-27 | **Status**: Merged (#176, 2026-09-26 UTC) | **Issue**: #172 (closes it)

**Input**: Issue #172 - a returned and refunded parcel left the seller's Insights but stayed in the administrator's
Overview.

## Why

A parcel that was returned and refunded (`parcel_returns.Status = 'Received'`, specs/066) is not revenue on a
seller's Insights page (specs/068). It still was on the administrator's Overview: the order's whole
`TotalAmount` stayed in the daily revenue, the returned product stayed in the top products, and the buyer's
spending was unchanged. Two screens disagreed about one sale.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Revenue is what the shop kept (Priority: P1)

An administrator reads the Overview's revenue for a day on which one parcel of an order came back and was
refunded. The day's revenue is the orders' totals less that refund; the order still counts as one order.

**Why this priority**: Revenue is the headline number of the page, and the seller's page already left the refund
out; an administrator comparing the two saw a discrepancy that looked like a bug.

**Independent Test**: One order with two sellers' parcels, one returned and received with a refund of 22,000; the
day's revenue is the order's total less 22,000, and the order count is unchanged.

**Acceptance Scenarios**:

1. **Given** an order paid on day D with a parcel whose return was received with a refund R, **When** revenue for D
   is read, **Then** it is the sold orders' totals less R, in that order's currency.
2. **Given** the same order, **When** the order count for D is read, **Then** the order still counts once: its
   delivery was kept.
3. **Given** another order whose return is still open, **When** revenue is read, **Then** that order counts in
   full: the return may yet be refused.

---

### User Story 2 - Top products leave out what came back (Priority: P2)

The returned parcel's lines leave the Overview's top products; the other seller's lines of the same order stay.

**Why this priority**: The second list on the page; a returned product at the top of "best selling" misleads
buying decisions. Second because it is not money.

**Independent Test**: In the same order, the returned seller's product is absent from top products and the other
seller's product is present.

**Acceptance Scenarios**:

1. **Given** a parcel returned and received, **When** top products are read, **Then** the lines of that parcel -
   the lines of its seller in that order - are not counted.
2. **Given** the shop's own parcel returned, **When** top products are read, **Then** the lines with no seller in
   that order are not counted (the shop's parcel has no seller).

---

### User Story 3 - A buyer's spending is less their refunds (Priority: P3)

The Overview's top buyers show each buyer's spending less what was refunded to them.

**Why this priority**: The third list; it follows from the same refund.

**Independent Test**: The buyer of the returned order's spending is the order's total less the refund.

**Acceptance Scenarios**:

1. **Given** a buyer whose parcel was returned and received, **When** top buyers are read, **Then** their spending
   in that currency is less the refund, and their order count is unchanged.

---

### Edge Cases

- **A return still open** (requested, accepted, refused, escalated, sent back): changes nothing. Only `Received`
  means refunded.
- **A refund received after the period was read.** The refund counts on the day the order was paid, so a period
  already past can go down when a return is received. The seller's page has always done the same; documented.
- **Delivery.** The refund is goods and their tax, never delivery (specs/066), so the order keeps its delivery
  charge in revenue.
- **Currencies.** A refund is subtracted in its order's currency and never across currencies.
- **A return with no recorded amount.** `RefundAmount` null counts as 0.
- **Orders not sold** (failed, cancelled, settling): not in the period at all, so their returns do not matter.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** Admin revenue, per day and in total, is less the `RefundAmount` of every parcel of a sold order in
  the period whose return was received. The refund is goods and their tax, never delivery.
- **FR-002** A buyer's spending in the top buyers is less the same refunds.
- **FR-003** The lines of a returned parcel leave the top products. A line belongs to its seller's parcel
  (specs/035), and the shop's own parcel has no seller.
- **FR-004** The order still counts as one order, because its delivery was kept. A return that is still open
  changes nothing: it may yet be refused.
- **FR-005** The response shapes do not change; the storefront needs no change.

### Key Entities

- **Sold order**: `OrderInsights.Sold` - Paid, Completed, Preparing, Shipped - dated by `PaidAt ?? CreatedAt` in
  the shop's days (specs/072, 082).
- **Received return**: a `parcel_returns` row with `Status = 'Received'` and its `RefundAmount` (specs/066).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For one order with two sellers' parcels, one returned with a refund of 22,000, the Overview's revenue
  is exactly the order totals less 22,000 and the seller's page agrees (the new test was red first by exactly that
  22,000).
- **SC-002**: The returned seller's product is absent from top products; the other seller's is present.
- **SC-003**: The buyer's spending is the order's total less 22,000.
- **SC-004**: Removing any one of the three subtractions turns the test red (three mutations).
- **SC-005**: No regression: Order 266/266, Bruno 267/267 requests and 437/437 tests at the merge.

## Assumptions

- `Received` is the one state that means refunded; the refund amount is written when the parcel is received
  (specs/066).
- A line belongs to the parcel of its seller in the same order (specs/035), so "the lines of a returned parcel" is
  decidable from `order_items.SellerId` and `order_shipments.SellerId`.

## Decisions

- **The refund counts on the day the order was paid**, like the rest of the order and like the seller's page.
  Dating it on the day the parcel came back would split one sale across two periods. As a result, a period already
  past can go down when a return is received.
- **Net revenue, with no separate "refunded" figure.** The seller's page shows net revenue alone. A refunded total
  would need a new field on both pages, and is recorded as a known limit.
- **The refunds are a second, grouped query.** A scalar subquery summed inside the GROUP BY is what SQL Server
  refuses and PostgreSQL may reshape; two plain grouped queries are merged by key in memory.

The full reasoning is in [research.md](./research.md).

## Out of scope

- Showing how much was refunded in a period.
