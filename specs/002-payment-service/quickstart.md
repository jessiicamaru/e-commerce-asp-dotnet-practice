# Quickstart: Validating the Payment Service

**Feature**: [spec.md](./spec.md) | **Contracts**: [messages.md](./contracts/messages.md), [http-api.md](./contracts/http-api.md)

Each scenario maps to a success criterion, so a failure here is a failure of the spec.

---

## Prerequisites

```bash
cd server
docker compose up -d                 # now includes postgres-payment on 5438
dotnet ef database update --project src/Services/Payment/Ecommerce.Payment.Infrastructure/ \
                          --startup-project src/Services/Payment/Ecommerce.Payment.WebApi/
./start-dev.ps1                      # six services; Payment on 5061
```

`server/.env` needs `PAYMENT_DB_PORT=5438` and `PAYMENT_OUTCOME=Approve`.

---

## Scenario 1 — Checkout completes, for the first time (US1, SC-001, SC-007)

The whole point of the feature.

1. Create a product, stock it with 10 units.
2. Submit an order for 3 as a customer.
3. Watch the order, without publishing anything by hand.

**Expected**, within a few seconds and with no manual intervention:

- The saga reaches `OrderCompleted` and finalizes — no instance left behind.
- `GET /api/stock/{productId}` shows on hand **7**, reserved **0**. The units left the building
  rather than returning to the shelf.
- `GET /api/payments/{orderId}` shows `Approved`, the order's total, and `provider: "Stub"`.

Before this feature the same steps ended with the reservation expiring and the stock coming back.

---

## Scenario 2 — A payment can be accounted for (US2, SC-006)

`GET /api/payments/{orderId}` as an admin returns the amount, outcome, timestamp and provider.

`GET /api/payments/{unknown}` returns `404` with a readable message, not a stack trace.

Anonymous access to either returns `401`; a `Customer` token returns `403`.

---

## Scenario 3 — Replay charges once (US3, SC-004)

Redeliver the same `ProcessPaymentCommand` from the RabbitMQ management UI, twice.

**Expected**: one payment row, unchanged `ProcessedAt`, stock deducted once. A reply is published
each time — the saga may have missed the first — but nothing else changes.

---

## Scenario 4 — The compensation path finally runs (US4, SC-008)

The branch that has never executed.

1. Set `PAYMENT_OUTCOME=Reject` and restart the Payment service.
2. Confirm `GET /health` reports `configuredOutcome: "Reject"` — this is the check that stops a
   deliberately rejecting service looking like an outage.
3. Stock a product, submit an order, and watch.

**Expected**: the order fails; the saga publishes `ReleaseInventoryCommand`; the held units are
available again; the payment row reads `Rejected` with a reason naming the setting.

Then set it back to `Approve` and restart.

---

## Scenario 5 — An invalid amount is refused even when approving (FR-004)

With `PAYMENT_OUTCOME=Approve`, publish a `ProcessPaymentCommand` with `amount: 0` by hand.

**Expected**: `PaymentFailedEvent` with `Invalid amount 0 for order ...`, and a `Rejected` row that
records the amount that was refused. "Always succeeds" does not mean "approves nonsense".

---

## Scenario 6 — One payment under concurrency (SC-005)

Fire 50 simultaneous `ProcessPaymentCommand` messages for one order and assert exactly one payment
row exists, and that every request received a reply.

This belongs in the test project rather than a shell script, and must run against a real PostgreSQL:
the guarantee under test is the unique constraint, so an in-memory provider would pass against code
that double-charges.

---

## Scenario 7 — The stub is impossible to mistake (FR-008, FR-012)

Three independent signals, checked together:

- Startup log carries a warning naming the service a stand-in and stating its configured outcome.
- `GET /health` reports `provider` and `configuredOutcome`.
- Every row in `payments` has `Provider = 'Stub'`.

If any one of these is missing, the risk in [research D3](./research.md) is live.
