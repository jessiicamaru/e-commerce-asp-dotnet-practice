# Quickstart: Validating the saga payment timeout

> Written on 2026-09-27, after the feature merged (#135), from the code at that merge, the pull request and
> docs/architecture/saga-orchestration-roadmap.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/messages.md](contracts/messages.md)

## Prerequisites

```bash
cd server
docker compose up -d            # the saga database on 5436, Payment's on 5438, RabbitMQ
```

## Scenario 1 - The saga's transitions (US1, US2, US3)

```bash
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Orchestrator.Tests --filter "FullyQualifiedName~OrderStateMachineTests"
```

**Expected**: eight green tests through MassTransit's harness - `A_paid_order_completes`,
`A_rejected_payment_releases_the_stock_and_fails_the_order`,
`An_unanswered_payment_releases_the_stock_fails_the_order_and_waits_for_a_late_answer`,
`A_payment_approved_after_the_timeout_is_refunded_and_the_order_stays_failed`,
`A_payment_rejected_after_the_timeout_needs_nothing`, `A_timeout_after_the_order_completed_changes_nothing`,
`A_repeated_timeout_fails_the_order_once` (and is not faulted), and
`The_state_the_sweeper_looks_for_is_the_one_the_saga_stores`.

## Scenario 2 - The sweeper and the options (FR-001, FR-005, SC-003)

```bash
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Orchestrator.Tests --filter "FullyQualifiedName~PaymentTimeoutTests"
```

**Expected**: green, against PostgreSQL on 5436 - only orders still in `InventoryReservedState` past the cutoff
are due; expiring announces each due order through the outbox; the defaults are 10 minutes and 30 seconds; a
timeout not shorter than the hold refuses to start; `0`, `-5` and `ten minutes` refuse to start. The whole
project: 15 tests at the merge.

## Scenario 3 - Payment refunds a late approval once (US2, SC-002)

```bash
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Payment.Tests --filter "FullyQualifiedName~RefundTests"
```

**Expected**: `A_payment_approved_after_the_order_failed_is_refunded_once_and_says_why` - the first
`RefundOrderCommand` returns true, the second false, one refund row of the full amount, and the audit entry's
summary contains the reason. The whole project: 20 tests at the merge.

## Scenario 4 - The startup check (SC-003)

```bash
ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS=900 INVENTORY_RESERVATION_TTL_MINUTES=15 \
  dotnet run --project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/
```

**Expected**: the orchestrator does not start; the exception says `ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS (900s)
plus one sweep (30s) must be shorter than Inventory's hold ...`.

## Scenario 5 - End to end on the compose stack (US1, US2, SC-001, SC-002)

What the pull request ran:

1. Set `ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS=30` (the pull request does not record where it set it;
   `server/.env` is read by compose) and rebuild the orchestrator and Payment:
   `docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build orchestrator payment`.
2. `docker stop ecommerce-payment` (the container name in `docker-compose.app.yml`), then place a real order as a
   customer (`POST /api/orders` through :5000).
3. While waiting, `GET /api/stock/{variantId}`: held (the run recorded `onHand=24 reserved=1`).
4. After the timeout plus a sweep: the order is `Failed` (`GET /api/orders/{id}`) and `reserved=0` with `onHand`
   unchanged. The run recorded about 55 seconds.
5. `docker start ecommerce-payment`: it approves the queued charge; exactly one refund of the full amount is
   recorded (`SELECT * FROM refunds WHERE "OrderId" = '<id>';` on 5438 - the run's was ₫20,416,000); the order
   stays `Failed`, no stock moves, the cart keeps its line.
6. In Seq, `OrderId = '<id>'` shows the four transitions:

```text
Saga …: submitted; reserving inventory
Saga …: inventory reserved; requesting payment
Saga …: payment did not answer in time; releasing inventory, order failed
Saga …: payment approved after the order failed; refunding
```

An earlier run restarted Payment **before** the timeout by mistake: the order completed normally and the stock
was deducted (25 → 24) - the race the other way (US2 scenario 2).

## Scenario 6 - Mutation checks

The pull request's, each restored:

| Mutation | Expected |
| :-- | :-- |
| No `Ignore` for a repeated timeout | 1 red (the fault assertion) |
| The timeout does not release the stock | 3 red |
| A late approval also completes the order | 1 red |
| The query ignores the cutoff | 1 red |
| No hold check | 1 red |
