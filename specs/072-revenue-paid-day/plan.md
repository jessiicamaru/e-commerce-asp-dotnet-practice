# Implementation Plan: Revenue counts on the day an order was paid

> Completed on 2026-09-27, after the feature merged (#156), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Branch**: `072-revenue-paid-day` | **Spec**: [spec.md](spec.md) | **Issue**: #116 | **PR**: #156 (merged 2026-09-26)

## Summary

Record when an order was paid, in the settlement's own guarded statement, and date every insight's sales by it,
falling back to when the order was placed for orders from before.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: EF Core with Npgsql, MediatR, MassTransit (the saga's `OrderCompletedEvent`)

**Storage**: `ecommerce_order_db` - one nullable column, migration `AddOrderPaidAt`

**Testing**: xUnit against real PostgreSQL (5434); `verify-saga.sh` live

**Target Platform**: Order service

**Constraints**: expand-only schema change; the period filter and the day grouping must agree

**Scale/Scope**: one column, three expressions, one response field

## Design

- `Order.PaidAt` (`DateTime?`). Migration `AddOrderPaidAt` adds a nullable column (expand only). There is no
  index: the reports filter on `COALESCE("PaidAt", "CreatedAt")`, which a plain index cannot serve, and the
  reports already scan the period (#113 is the index work).
- `OrderRepository.SettleAsync`: the same guarded `UPDATE ... WHERE "Status" = 'Submitted'` also sets `PaidAt`
  to `settledAt` when it settles to `Paid`, and leaves it null when the order failed.
- `OrderInsights`: `SoldIn` filters on `(o.PaidAt ?? o.CreatedAt)`. Revenue groups by that date's day.
  `SellerLinesIn` takes its `Day` from it too. The same `PaidAt ?? CreatedAt` is written in all three places,
  because EF cannot share one expression into an anonymous `GroupBy` key. `RevenueDayTests` holds them together:
  a period and its days that disagree is exactly what #125 was.
- `OrderDetailResponse.PaidAt`, mapped in `OrderMapping.ToDetail`.

(From the code: `SettleAsync` is the private method behind `TrySettleAsync`, and `settledAt` is
`CompleteOrderCommand.CompletedAt`, which the consumer takes from `OrderCompletedEvent.CompletedAt`. The comment on
`SoldIn` in `OrderInsights.cs` says "InsightsTests holds them together"; the test that does is `RevenueDayTests`.)

## Research

- **D1 - no backfill.** `UpdatedAt` is rewritten by every later parcel move, so it is not the payment time.
  `CreatedAt` is the old behaviour, so falling back to it changes nothing for old orders.
- **D2 - the saga's time, not the database's `now()`.** `CompleteOrderCommand` carries the moment the saga
  completed, which is the moment of payment. A redelivery arriving later is refused by the guard, so it cannot
  move the date.

With alternatives, and D3 on why there is no index: [research.md](research.md).

## Constitution Check

The plan as first written had no Constitution Check; it was added in the backfill. Against all five principles of
[constitution.md](../../.specify/memory/constitution.md):

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order records its own settlement time from the event it already consumes; no other service is asked |
| **II. Clean Architecture Layering** | **Pass.** A domain property, a repository statement and a read query - each in its layer |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** `PaidAt` is written by the same guarded `UPDATE ... WHERE "Status" = 'Submitted'` that settles the order, so a redelivered completion affects zero rows and cannot move the date (tested: `The_payment_time_is_written_once_by_the_settlement_and_never_for_a_failure`) |
| **IV. Identity Comes From the Token** | **Pass (not applicable).** No endpoint's access changed; the detail gains a field within its existing owner scope |
| **V. Evidence Over Assumption** | **Pass.** 3 tests against real PostgreSQL, 6 of 6 mutations caught - and the PR records that a first run counted a build error as a catch and the mutation was rerun with valid syntax; live `PaidAt` values read after `verify-saga.sh` |

**Schema evolution**: expand only - one nullable column an earlier image never writes.

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/072-revenue-paid-day/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── contracts/http-api.md
└── checklists/requirements.md
```

### Source Code (touched at the merge)

```text
server/src/Services/Order/
├── Ecommerce.Order.Domain/Entities/Order.cs                                    # PaidAt
├── Ecommerce.Order.Infrastructure/Migrations/20260926085159_AddOrderPaidAt.cs
├── Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderRepository.cs  # SettleAsync writes it
├── Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderInsights.cs    # PaidAt ?? CreatedAt, three places
└── Ecommerce.Order.Application/Orders/Common/{OrderResponses.cs, OrderMapping.cs}
server/tests/Ecommerce.Order.Tests/{RevenueDayTests.cs (new, 3), VoucherCheckoutTests.cs}
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| - | - | - |

## What this feature does not finish

- Older orders keep counting on the day they were placed (no backfill, D1).
- The date expression is repeated in three places; a fourth insight must repeat it too, and `RevenueDayTests` is what
  notices if it does not.
