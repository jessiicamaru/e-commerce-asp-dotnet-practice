# Research: Revenue counts on the day an order was paid

> Written on 2026-09-27, after the feature merged (#156), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](spec.md) | **Date of the decisions**: 2026-09-26 (decision 53 in
[docs/project/decisions.md](../../docs/project/decisions.md))

D1 and D2 were first recorded in [plan.md](plan.md#research); D3 comes from the plan's design notes. Who decided is not
recorded.

> An alternative marked *(reconstructed)* was not recorded when the decision was taken; it is the obvious other
> choice, named in the backfill so the decision reads whole, and its reason for rejection is inferred from the code
> and the recorded rationale.

---

## D1 - No backfill

**Decision**: Orders from before this have `PaidAt` null and are dated by `CreatedAt`.

**Rationale**: Nothing recorded when they were paid. `UpdatedAt` is rewritten by every later parcel move, so it is not
the payment time. `CreatedAt` is the old behaviour, so falling back to it changes nothing for old orders.

**Alternatives considered**:

- **Backfill from `UpdatedAt`.** Rejected: it would date a shipped order by its last parcel move.

---

## D2 - The saga's time, not the database's `now()`

**Decision**: `PaidAt` = `settledAt`, which is `CompleteOrderCommand.CompletedAt` from `OrderCompletedEvent`.

**Rationale**: That is the moment the saga completed, which is the moment of payment. A redelivery arriving later is
refused by the settlement's guard, so it cannot move the date.

**Alternatives considered**:

- **`now()` in the `UPDATE`.** Rejected: an order settled late by a slow consumer would be dated by the consumer, not by
  the payment.

---

## D3 - No index, and the expression written three times

**Decision**: No index on `PaidAt`. The filter (`SoldIn`), the admin's day grouping and `SellerLinesIn`'s `Day` each
write `PaidAt ?? CreatedAt`; `RevenueDayTests` holds them together.

**Rationale**: The reports filter on `COALESCE("PaidAt", "CreatedAt")`, which a plain index cannot serve, and they
already scan the period (#113 was the index work). EF cannot share one expression into an anonymous `GroupBy` key, so
the expression is repeated; a test that places and pays on different days fails if any of the three drifts - the
mutations "the period filtered on `CreatedAt`", "the admin grouped by `CreatedAt`" and "the seller grouped by
`CreatedAt`" are each caught.

**Alternatives considered**:

- *(reconstructed)* **A stored "sale day" column.** Rejected: a derived value to keep in step with two others.

> The earlier task list spoke of "one `SaleDay`"; the code has no such helper - the three expressions above are what
> merged.
