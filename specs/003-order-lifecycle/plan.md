# Implementation Plan: Order Lifecycle Visibility

**Branch**: `003-order-lifecycle` | **Date**: 2026-09-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-order-lifecycle/spec.md`, tracked as issue
[#2](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/2)

## Summary

Give `Ecommerce.Order` ears and a window. Two consumers settle an order when the checkout
orchestrator announces its outcome, and two query endpoints let the shopper who placed an order read
it back.

This is the smallest feature so far in terms of new code and the one with the widest reach: it makes
the order record — the thing a shopper and an operator actually look at — agree with what the rest of
the system already did. Today it does not. Eleven completed orders read `Submitted`.

Three things shape the design beyond the obvious:

- **The Order service already has an inbox and does not use it.** Its initial migration created
  `InboxState`, `OutboxState` and `OutboxMessage`; what is missing is the endpoint callback that
  wires consumers to them (research D3). So idempotency needs no migration — a fact established by
  reading the migration, not assumed from the pattern elsewhere.
- **A read-modify-write settle is not idempotent, however careful it looks.** Two deliveries can
  both read `Submitted` and both write. The guard has to be in the `WHERE` clause (research D1).
- **Hiding another shopper's order is a query shape, not a check.** Filtering by owner and returning
  "not found" is a different thing from loading by id and then comparing — the second discloses that
  the id exists (research D5).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MassTransit 8.3.6 (RabbitMQ transport, EF Core inbox), MediatR 12.4.1,
FluentValidation 12.1.1, Npgsql EF Core 10.0.3, `Ecommerce.Shared` (auth, exceptions, validation
behavior), `Ecommerce.Contracts`

**Storage**: PostgreSQL 16, existing database `ecommerce_order_db`, host port 5434. One new index;
no new table

**Testing**: xUnit with MassTransit's test harness, against a real PostgreSQL — a third test project,
`Ecommerce.Order.Tests`, mirroring `Ecommerce.Payment.Tests` including its bounded connection pool
(research D7)

**Target Platform**: Linux container / Windows dev host, HTTP on the existing port 5059

**Project Type**: Existing backend microservice, Clean Architecture — Domain, Application,
Infrastructure, WebApi. No new projects under `src/`

**Performance Goals**: A settled order is readable within 5 seconds of the checkout finishing at the
99th percentile (SC-001). The order list is a filtered, sorted, paged read and gets an index to match

**Constraints**: A settled order is immutable (FR-004, FR-005) — enforced in the `WHERE` clause, not
in application logic. The reader's identity comes from the token and never from the request
(FR-009). An order belonging to someone else is indistinguishable from one that does not exist
(FR-010)

**Scale/Scope**: Two consumers, two queries, zero new entities, zero new message contracts, one
index migration. Smaller than feature 002

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.0.0.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The order's status is a fact this service owns and nobody else records; the saga's `OrderStateData` tracks the *checkout's* progress, which is a different fact with a different lifetime (it is finalized and discarded). Order learns the outcome by message, never by reading the saga's or Payment's database. No new shared contract is introduced — the announcements already exist |
| **II. Clean Architecture Layering** | **Pass.** Consumers live in WebApi and do nothing but dispatch through MediatR, matching Inventory. The guarded settle is expressed as a repository method declared in `Application/Common/Interfaces/` and implemented in Infrastructure, so the Application layer states the guarantee it needs without naming SQL |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** These consumers publish nothing, so there is no publish-ordering hazard to get wrong; the whole of the principle that applies here is idempotency. It is enforced by a guarded transition — `UPDATE ... WHERE Id = @id AND Status = 'Submitted'` — so a redelivery affects **zero rows** rather than re-applying the effect, which is the principle's wording exactly. The inbox is added as well, but as a shortcut for the common case, not as the guarantee (research D1, D3) |
| **IV. Identity Comes From the Token** | **Pass.** Both queries take no user id. The handler reads `ICurrentUser.Id`; the controller is `[Authorize]`. The detail query filters by owner inside the query rather than checking ownership after loading, so there is no code path where a supplied id selects a row that is then rejected (research D5) |
| **V. Evidence Over Assumption** | **Pass, with one thing named as unverified.** The idempotency tests run against a real PostgreSQL because the guarantee is a `WHERE` clause the database evaluates, and the redelivery test is mutation-checked: removing the status guard must make it fail. What is **not** verified end to end is the token → `ICurrentUser` → owner-filter path against a real signed token; the tests substitute `ICurrentUser`. That is the same class of gap that produced the role-claim incident this constitution cites, so it is recorded here and carried into `tasks.md` as an explicit optional task rather than left implicit |

**Post-Phase 1 re-check**: no violations. Complexity Tracking is empty.

One judgement worth stating rather than burying: **this feature deliberately leaves two status
values unreachable.** `StockReserved` and `Paid` stay in the enum with nothing able to produce them.
That is a knowing choice (the user's, on 2026-09-16) rather than an oversight, and FR-012 turns it
into a documentation obligation so the next reader does not have to rediscover it. It is not a
constitution violation — no principle requires an enum to be fully reachable — but it is the kind of
thing that looks like a bug six months from now if nobody wrote down that it was decided.

## Project Structure

### Documentation (this feature)

```text
specs/003-order-lifecycle/
├── plan.md              # This file
├── spec.md              # 3 user stories, 12 requirements, 6 success criteria
├── research.md          # Phase 0: seven decisions with rejected alternatives
├── data-model.md        # Phase 1: no new entities; the status transition table and the index
├── quickstart.md        # Phase 1: validation scenarios
├── checklists/
│   └── requirements.md  # Spec quality checklist (all items pass)
├── contracts/
│   ├── messages.md      # Consumed announcements; nothing published
│   └── http-api.md      # Order list and order detail
└── tasks.md             # Phase 2 — created by /speckit-tasks
```

### Source Code (repository root)

```text
server/src/Services/Order/
├── Ecommerce.Order.Domain/
│   └── Enums/OrderStatus.cs              # documented, not changed
├── Ecommerce.Order.Application/
│   ├── Common/Interfaces/
│   │   └── IOrderRepository.cs           # + guarded settle, + paged read, + owner-scoped read
│   ├── Orders/Commands/
│   │   ├── CompleteOrder/                # NEW — guarded Submitted -> Completed
│   │   └── FailOrder/                    # NEW — guarded Submitted -> Failed + reason
│   ├── Orders/Queries/
│   │   ├── GetMyOrders/                  # NEW — paged, owner-scoped
│   │   └── GetMyOrderById/               # NEW — one order, owner-scoped
│   └── Orders/Common/                    # NEW — shared response records
├── Ecommerce.Order.Infrastructure/
│   ├── Persistence/Repositories/         # guarded UPDATE, paged query
│   ├── Persistence/Configurations/       # + (UserId, CreatedAt DESC) index
│   └── Migrations/                       # one new migration, index only
└── Ecommerce.Order.WebApi/
    ├── Consumers/                        # NEW — OrderCompletedConsumer, OrderFailedConsumer
    ├── Controllers/OrdersController.cs   # + GET /, + GET /{id}
    └── Program.cs                        # + AddConsumer x2, + inbox endpoint callback

server/tests/
└── Ecommerce.Order.Tests/                # NEW
```

Touched outside the Order service:

- `server/Ecommerce.slnx` — one new test project
- `.github/workflows/ci.yml` — a `postgres-order` service container for the new tests
- `CLAUDE.md`, `docs/` — the saga roadmap and the service map describe a gap that this closes

**Structure Decision**: Everything stays inside the existing Order service. The two settle commands
are commands rather than queries because they change state, and they are foldered by use case like
every other feature in the repository. Response records go in `Orders/Common/` because the list and
the detail share them — today `OrderResponse` lives beside `SubmitOrderCommand`, which is why
nothing else can reference it without importing a command namespace.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature finishes, and what it does not

**Finishes**: the order record stops lying. A completed order says completed, a failed order says
why, and a shopper can read either without database access. The last participant in the checkout
saga that was not listening now listens.

**Does not**: show progress *during* checkout. An order is submitted, and then some seconds later it
is completed or failed; there is no "stock reserved, awaiting payment" for anyone watching. Nor does
it give an operator a cross-shopper view, or a shopper any way to cancel. Those are separate
features, and this plan deliberately does not reserve space for them beyond leaving the enum values
in place.
