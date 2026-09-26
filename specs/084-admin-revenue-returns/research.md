# Research: Admin revenue less returns

> Written on 2026-09-27, after the feature merged (#176), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-27

Four decisions. D1-D3 are the spec's Decisions, expanded; D1 is also
[decisions.md row 66](../../docs/project/decisions.md). D4 is read from the code.

---

## D1 - The refund counts on the day the order was paid

**Decision**: a received return is subtracted on the shop day of its order's `PaidAt ?? CreatedAt`, and only when
that order is in the period.

**Rationale**: that is how the seller's page already counts it (specs/068), and one sale must not be split across
two periods. Consequence, accepted and documented: a period already past can go down when a return is received.

**Alternatives considered**:

- **Date the refund by `ReceivedAt`.** Rejected: the sale would count in full in one period and negatively in
  another, and the two pages would disagree again.

---

## D2 - Net revenue, no "refunded" figure

**Decision**: revenue and spending are reported net. No new field.

**Rationale**: the seller's page shows net revenue alone. A refunded total would need a new field on both pages
and in both responses. Recorded as a known limit.

**Alternatives considered**:

- **A `refunded` amount beside revenue.** Deferred (out of scope).

---

## D3 - The refunds are a second grouped query, merged in memory

**Decision**: `RevenueByDayAsync` and `BuyersAsync` keep their grouped query over sold orders and run a second
grouped query over `ReturnedIn(from, to)` with the same keys - (shop day, currency) and (buyer, currency) - then
subtract by key in memory with `GetValueOrDefault`.

**Rationale**: a scalar subquery summed inside the GROUP BY is what SQL Server refuses and PostgreSQL may reshape;
two plain grouped queries are merged by key in memory. The number of keys is the number of days (or buyers) times
currencies, so the merge is small.

**Alternatives considered**:

- **One query with a correlated subquery per order.** Rejected for the reason above.

---

## D4 - The lines of a returned parcel are found by seller

**Decision**: top products drop an order line when an `order_shipments` row of the same order with the line's
`SellerId` has a `Return` whose status is `Received`. `ReturnedParcel` is a private class with init properties, not
a record, so EF can group over it (the same reason as `SellerLine`).

**Rationale**: a line belongs to its seller's parcel (specs/035); the shop's own parcel has `SellerId` null, and
EF's null semantics make `s.SellerId == item.SellerId` match null to null, so the shop's own returned lines drop too.
The order count is not touched, because the order's delivery was kept.

**Alternatives considered**:

- **Subtract a returned line's value from revenue per product.** Not recorded as considered; dropping the lines is
  what the seller's page does.

---

## Known limit

A period's revenue can go down after the fact when a return is received (D1), and the page does not show how much
was refunded (D2).
