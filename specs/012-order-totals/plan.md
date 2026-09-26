# Implementation Plan: A Total With Something Behind It

> Completed on 2026-09-27, after the feature merged (#32), from the code at that merge, the pull
> request and docs/features/shopping-and-checkout.md.

**Branch**: `012-order-totals` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/012-order-totals/spec.md`

## Summary

Order computes tax at checkout from the destination country (configured rates), per line and on
delivery, rounded half away from zero, and stores subtotal, delivery, tax, discount (0), grand total,
the rate, and per-line tax. The database enforces that the parts sum to the total. Payment already
charges `TotalAmount`; no contract changes. Decisions: [research.md](./research.md), ADR-002.

The approach keeps the arithmetic in one pure function, `OrderTotals.Compute`, so the rule that decides
what a customer pays can be unit-tested without a database, a broker or another service. The handler
only feeds it the lines Catalog priced (feature 009), the delivery option's price (feature 011) and
the rate for the address's country, then stores what it returns. The database is the second line of
defence: three CHECK constraints refuse a total whose parts disagree, a non-zero discount and an
impossible rate.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: EF Core with Npgsql (migration and CHECK constraints), MediatR 12.4.1,
`Microsoft.Extensions.Configuration` for the rates. No new package.

**Storage**: PostgreSQL 16, `ecommerce_order_db` on host port 5434 - five nullable columns and three
CHECK constraints (see [data-model.md](./data-model.md))

**Testing**: xUnit. `OrderTotalsTests` is pure arithmetic; `TotalsPersistenceTests` runs against the
real Order database, because the guarantee under test in one of its cases is a CHECK constraint.
End to end: `verify-saga.sh` recomputes the tax independently, and Bruno's `order/checkout` asserts
the parts sum.

**Target Platform**: The Order service (HTTP 5059), in a container or on the host

**Project Type**: One backend microservice changed, Clean Architecture

**Performance Goals**: None stated. The computation is a few multiplications per line and adds no
call to another service.

**Constraints**: The charge must be exactly the stored total (FR-008). The schema must stay usable by
the previous Order image (constitution, schema evolution). No change to any message contract.

**Scale/Scope**: One service (Order), one migration, one ADR. Orders at this stage are in the tens.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. The first version of
this table (written before the build) is kept below as it was; the five-principle table is the
completed form of the same assessment.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order owns what is charged, and the tax rates are Order's own configuration. No other service is asked for a rate, and no other database is read. Catalog still owns the net price (feature 009); Order only adds tax to it |
| **II. Clean Architecture Layering** | **Pass.** `OrderTotals` (pure) and the `ITaxRates` interface live in Application; `ConfiguredTaxRates`, which reads configuration, lives in Infrastructure and is registered in its `DependencyInjection.cs`. The controller did not change |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The parts are computed before anything is staged, then written with the order row and the `OrderSubmittedEvent` outbox message in the one `SaveChangesAsync`. No consumer changed, so no new idempotency question arises |
| **IV. Identity Comes From the Token** | **Pass.** Unchanged - the feature takes no new input from the client at all. The destination comes from the address Identity returns for the caller's own token (feature 011) |
| **V. Evidence Over Assumption** | **Pass.** The half-cent rounding case has a test, and a negative control (`AwayFromZero` → `ToEven`) turned 2 of 6 arithmetic tests red. The sum constraint is tested by an insert the real database refuses. The charge is checked end to end by `verify-saga.sh`, which recomputes the tax itself rather than trusting Order's figure |

The table as first written:

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

**Post-Phase 1 re-check**: no violations. The design added nothing a principle forbids: no
cross-service read, no new message, no new write path outside the existing checkout transaction. The
migration only adds nullable columns and constraints that let a NULL part through, so an image from
before this feature can still insert orders - the rule the constitution added in v1.1.0.

## Project Structure

### Documentation (this feature)

```text
specs/012-order-totals/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Six decisions: tax-exclusive prices, rates, rounding, storage, contracts, proof
├── data-model.md        # The five columns, three constraints and the backfill
├── quickstart.md        # Validation scenarios with the exact commands
├── contracts/
│   ├── http-api.md      # The order and checkout responses gain the parts
│   └── messages.md      # No message changed, and why
├── checklists/
│   └── requirements.md  # Spec quality checklist (all items pass)
└── tasks.md             # Task list, all done
```

### Source Code (repository root)

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

The files, as the pull request lists them:

- `Ecommerce.Order.Domain/Entities/Order.cs`, `OrderItem.cs`
- `Ecommerce.Order.Application/Common/Interfaces/ITaxRates.cs`
- `Ecommerce.Order.Application/Orders/Common/OrderTotals.cs`, `OrderResponses.cs`, `OrderMapping.cs`
- `Ecommerce.Order.Application/Orders/Commands/SubmitOrder/SubmitOrderCommand.cs` (response records),
  `SubmitOrderCommandHandler.cs`
- `Ecommerce.Order.Infrastructure/Tax/ConfiguredTaxRates.cs`, `DependencyInjection.cs`,
  `Persistence/Configurations/OrderConfiguration.cs`, `OrderItemConfiguration.cs`
- `Ecommerce.Order.Infrastructure/Migrations/20260921182829_AddOrderTotals.cs` (+ designer, snapshot)
- `Ecommerce.Order.WebApi/Program.cs` (resolves `ITaxRates` at startup), `appsettings.json` (`Tax`)
- `server/tests/Ecommerce.Order.Tests/OrderTotalsTests.cs`, `TotalsPersistenceTests.cs`,
  `CheckoutShippingTests.cs` (totals now include tax), `OrderTestFixture.cs` (rates)
- `.github/scripts/verify-saga.sh`, `bruno/order/checkout.yml`
- `docs/architecture/adr-002-tax-exclusive-prices.md`, `microservices-design.md`,
  `saga-orchestration-roadmap.md`, `docs/guides/getting-started.md`, `docs/README.md`, `CLAUDE.md`

**Structure Decision**: No new project and no new folder convention. The pure calculation sits in
`Orders/Common/` beside the other shared order code, because both the command and the responses use
it; the rate lookup follows the `IShippingOptions` / `ConfiguredShippingOptions` pair feature 011
introduced, one interface in Application and one configuration-backed class in Infrastructure,
validated when the service starts.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- **Discounts.** The part exists and is pinned to 0 by a constraint; applying one is later work (it
  arrived with vouchers in specs/069, which replaced `CK_orders_no_discount_yet` with a non-negative
  check).
- **Showing the total before the order exists.** The parts are stored and returned once an order is
  placed; a customer cannot see them *before* committing. That needed a quote endpoint, added in
  specs/018 (`GET /api/orders/quote`, priced by the same code).
- **Currencies, regional or per-category rates, invoices.** Out of scope, as the spec says. One
  currency held until specs/022.
