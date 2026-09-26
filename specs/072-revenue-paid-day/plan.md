# Implementation Plan: Revenue counts on the day an order was paid

**Branch**: `072-revenue-paid-day` | **Spec**: [spec.md](spec.md) | **Issue**: #116

## Design

- `Order.PaidAt` (`DateTime?`). Migration `AddOrderPaidAt` adds a nullable column (expand only), and an index
  on `PaidAt` for the reports' range.
- `OrderRepository.SettleAsync`: the same guarded `UPDATE ... WHERE "Status" = 'Submitted'` also sets `PaidAt`
  to `settledAt` when it settles to `Paid`, and leaves it null when the order failed.
- `OrderInsights`: `SoldIn` filters on `(o.PaidAt ?? o.CreatedAt)`. Revenue groups by that date's day.
  `SellerLinesIn` takes its `Day` from it too. There is one expression, `SaleDay`, so the filter and the grouping
  cannot disagree: that disagreement is exactly what #125 was.
- `OrderDetailResponse.PaidAt`, mapped in `OrderMapping.ToDetail`.

## Research

- **D1 - no backfill.** `UpdatedAt` is rewritten by every later parcel move, so it is not the payment time.
  `CreatedAt` is the old behaviour, so falling back to it changes nothing for old orders.
- **D2 - the saga's time, not the database's `now()`.** `CompleteOrderCommand` carries the moment the saga
  completed, which is the moment of payment. A redelivery arriving later is refused by the guard, so it cannot
  move the date.
