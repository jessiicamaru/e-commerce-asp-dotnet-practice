# Feature Specification: Admin revenue less returns

**Feature Branch**: `084-admin-revenue-returns` | **Created**: 2026-09-27 | **Issue**: #172 (closes it)

## Why

A parcel that was returned and refunded (`parcel_returns.Status = 'Received'`, specs/066) is not revenue on a
seller's Insights page (specs/068). It still was on the administrator's Overview: the order's whole
`TotalAmount` stayed in the daily revenue, the returned product stayed in the top products, and the buyer's
spending was unchanged. Two screens disagreed about one sale.

## Requirements

- **FR-001** Admin revenue, per day and in total, is less the `RefundAmount` of every parcel of a sold order in
  the period whose return was received. The refund is goods and their tax, never delivery.
- **FR-002** A buyer's spending in the top buyers is less the same refunds.
- **FR-003** The lines of a returned parcel leave the top products. A line belongs to its seller's parcel
  (specs/035), and the shop's own parcel has no seller.
- **FR-004** The order still counts as one order, because its delivery was kept. A return that is still open
  changes nothing: it may yet be refused.

## Decisions

- **The refund counts on the day the order was paid**, like the rest of the order and like the seller's page.
  Dating it on the day the parcel came back would split one sale across two periods. As a result, a period already
  past can go down when a return is received.
- **Net revenue, with no separate "refunded" figure.** The seller's page shows net revenue alone. A refunded total
  would need a new field on both pages, and is recorded as a known limit.
- **The refunds are a second, grouped query.** A scalar subquery summed inside the GROUP BY is what SQL Server
  refuses and PostgreSQL may reshape; two plain grouped queries are merged by key in memory.

## Out of scope

- Showing how much was refunded in a period.
