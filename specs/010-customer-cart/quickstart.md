# Quickstart & Validation: Somewhere to Put What You Intend to Buy

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-21

Eight scenarios. **Scenario 5 and 6 are the ones most likely to be wrong while everything looks
fine**, and both are about timing.

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
curl -s -o /dev/null -w 'cart REST %{http_code}\n' http://localhost:5062/health
grpcurl -plaintext localhost:6062 list
```

---

## Scenario 1 — A cart persists *(US1, FR-001–FR-004, SC-001)*

Sign in, add product X ×2, add X again ×1, add Y ×1. Sign in again with a **fresh token**.

**Expect**: `GET /api/cart` shows X×3 and Y×1 — one line for X, not two.

## Scenario 2 — Change and remove *(US1, FR-002)*

`PUT /api/cart/items/X` with `quantity: 5`; `DELETE /api/cart/items/Y`; then `PUT` X to `0`.

**Expect**: X×5 and no Y; then an empty cart. `quantity: -1` on add is a **400**.

## Scenario 3 — Checkout buys the cart *(US2, FR-005, SC-002)*

With X×3 in the cart, `POST /api/orders` with an **empty body**.

**Expect**: an order containing exactly X×3, at Catalog's price. The request carried no items — the
order came from the cart.

## Scenario 4 — The cart clears on completion, not before *(US2, FR-006, SC-003)*

After scenario 3, poll `GET /api/cart` until the order completes.

**Expect**: X is gone once the order is `Completed`. **Poll, don't read once** — removal follows the
completion event and is a second or two behind by design.

## Scenario 5 — What was added during checkout survives *(US2 sc.5, SC-005)*

Cart X×2. Start checkout; **before the order completes**, add Y×1 and raise X to 5.

**Expect** after completion: **X×3 and Y×1**. Not an empty cart. Removal is a decrement of what was
ordered, not "empty the cart".

This is the scenario that fails if the implementation takes the obvious shortcut.

## Scenario 6 — A declined payment leaves the cart alone *(US2 sc.3, FR-007, SC-004)*

Restart Payment with `PAYMENT_OUTCOME=Reject`. Cart X×2, check out.

**Expect**: the order ends `Failed` and the cart **still holds X×2**. The customer can check out
again — which is the whole reason removal waits for completion ([research D1](./research.md)).

## Scenario 7 — The cart never decides the charge *(US3, FR-009, SC-006)*

Cart X×1 at 9.99. Change X's price in Catalog to 50. Read the cart, then check out.

**Expect**: the cart **shows 50** (it stores no price; it asks), and the order is charged **50**.

## Scenario 8 — Privacy *(US4, FR-013, SC-007)*

Two customers, A and B, each with a cart. Each reads theirs. Then B tries every way to reach A's cart.

**Expect**: each sees only their own; there is no parameter to name another customer's cart;
anonymous is **401**.

---

## NEGATIVE CONTROLS — do not skip

**The events arrive out of order.** Apply `OrderCompletedEvent` for an order *before* its
`OrderSubmittedEvent` in the Cart tests. **Expect** the lines are still removed once the submission
arrives. Designed for only the likely order, the cart would silently never clear — the #15 shape.

**The same completion twice.** Deliver `OrderCompletedEvent` twice. **Expect** lines removed once;
anything added in between survives.

Both run against a real PostgreSQL in `Ecommerce.Cart.Tests`, because the guarantee is the row lock
and the guarded flag, and an in-memory provider would pass against code that double-removes.

## What passing all of this does not prove

- **That abandoned carts are cleaned up.** They are not, deliberately.
- **That guest carts work.** They do not exist.
- **That the customer sees the price they were shown.** They see the current price, and are charged
  the price at checkout. Quoting and honouring a price is a separate feature.
