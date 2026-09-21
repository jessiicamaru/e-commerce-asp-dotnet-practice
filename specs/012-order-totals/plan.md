# Implementation Plan: A Total With Something Behind It

**Branch**: `012-order-totals` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

## Summary

Order computes tax at checkout from the destination country (configured rates), per line and on
delivery, rounded half away from zero, and stores subtotal, delivery, tax, discount (0), grand total,
the rate, and per-line tax. The database enforces that the parts sum to the total. Payment already
charges `TotalAmount`; no contract changes. Decisions: [research.md](./research.md), ADR-002.

## Technical Context

**Language/Version**: C# / .NET 10 · **Storage**: PostgreSQL (Order, 5434) · **Testing**: xUnit
(pure arithmetic + real DB), `verify-saga.sh`, Bruno · **Scale**: one service changed.

## Constitution Check

| Principle | Assessment |
| :--- | :--- |
| I. Autonomy | ✅ Order owns what is charged; tax rates are its configuration. |
| II. Layering | ✅ `OrderTotals` (pure) and `ITaxRates` in Application; configuration in Infrastructure. |
| III. Atomic writes | ✅ The parts are computed before staging and written with the row and the event in one save. |
| IV. Identity | ✅ Unchanged — no new input from the client at all. |
| V. Evidence | ✅ Half-cent rounding test; DB CHECK tested by a failing insert; charge asserted end to end. |
| Persistence: invariants as constraints | ✅ CHECK on the sum, on discount = 0, on the rate range. |
| Schema evolution | ✅ Nullable columns; CHECK passes for rows an older image writes (research D4). |

No violations.

## Source Code

```text
server/src/Services/Order/
  Domain:          Order (+Subtotal, TaxTotal, DiscountTotal, TaxRate), OrderItem (+TaxAmount)
  Application:     Common/Interfaces/ITaxRates.cs; Orders/Common/OrderTotals.cs (pure);
                   SubmitOrder handler uses both; responses gain the parts
  Infrastructure:  Tax/ConfiguredTaxRates.cs; OrderConfiguration (+CHECKs); migration AddOrderTotals
  WebApi:          appsettings Tax section; startup resolves ITaxRates
server/tests/Ecommerce.Order.Tests: OrderTotalsTests (pure), TotalsPersistenceTests (DB), checkout tests
docs/architecture/adr-002-tax-exclusive-prices.md; verify-saga.sh; bruno; CLAUDE.md
```
