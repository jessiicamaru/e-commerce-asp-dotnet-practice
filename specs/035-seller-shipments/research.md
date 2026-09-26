# Research: Each seller ships their own part

> Completed on 2026-09-27, after the feature merged (#79), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. Decisions as written before the code; **Rationale** and
> **Alternatives considered** labels added so each reads like specs/001. The four decisions the issue
> left open (D2, D3, D4, D8) were taken in auto mode, as the checklist notes.

**Feature**: [spec.md](spec.md)

## D1 - A shipment table, one row per seller per order

**Decision**: `order_shipments (Id, OrderId, SellerId NULL, Status, TrackingReference, UpdatedAt)`,
unique on `(OrderId, SellerId)` with **`NULLS NOT DISTINCT`** (PostgreSQL 15+; the container runs 16).

**Rationale**: `SellerId` null is the shop's own part, as it is on `products` and `order_items`. Without
`NULLS NOT DISTINCT` two shop parts for one order would both be allowed, and the "create if missing"
statement below would create a second one every time it ran.

**Alternatives considered**: PostgreSQL's default `NULLS DISTINCT` unique index - rejected above.

## D2 - One model: the shop's goods are a part too

**Decision**: the shop's own lines form a shipment like any seller's, moved by an administrator.

**Rationale**: Two models - order-level fulfilment for the shop, part-level for sellers - would mean every read has
to ask which one an order uses. With one model the administrator's existing endpoints keep their
addresses and simply act on the shop's part. An order holding only the shop's goods has one part, and
behaves exactly as before: `verify-saga.sh` does not change.

An order with **no** shop goods has nothing for the administrator to ship: 409, saying each seller
ships their own. The administrator stays able to see every order; moderating a seller's part is a
different feature.

**Alternatives considered**: order-level fulfilment for the shop and part-level for sellers - rejected
above.

## D3 - Parts are created at checkout, AND on demand

**Decision**: `SubmitOrder` writes the parts in the same save as the order. Every fulfilment step
first runs one idempotent statement that creates any part that is missing:

```sql
INSERT INTO order_shipments (...)
SELECT <new id>, i."OrderId", i."SellerId", <status from the order>, <tracking if shipped>, now()
  FROM order_items i JOIN orders o ON o."Id" = i."OrderId"
 WHERE i."OrderId" = @order
 GROUP BY i."OrderId", i."SellerId", o."Status", o."TrackingReference"
ON CONFLICT ("OrderId", "SellerId") DO NOTHING;
```

The migration runs the same statement over every order (the backfill).

As built, the statement selects `DISTINCT "OrderId", "SellerId"` from `order_items` rather than the
`GROUP BY` sketched above - the same rule - and the migration carries its own copy on purpose: a
migration is a record of what ran and must not change when the repository's statement does.

**Rationale**: Why both: **a rolled-back image writes orders without parts.** The constitution requires that
redeploying the previous version keeps working, and an older Order places orders in the meantime.
When this version returns, those orders have no parts. A backfill that ran once at migration time
cannot see them; creating on demand can. The part starts in the state the order is in, so an order an
administrator shipped under the old version is a shipped part under the new one.

Reads tolerate a missing part the same way: a sale with no row of its own reads the order's status.

**Alternatives considered**: a one-off backfill only - rejected above, it cannot see orders an older
image writes after it ran.

## D4 - The order's own status is a summary, in words the old version knows

**Decision**: `orders.Status` stays, and is recomputed in the same transaction as every part move:

| Parts | orders.Status |
| :-- | :-- |
| none started | unchanged (`Paid`) |
| some started, not all shipped | `Preparing` |
| all shipped | `Shipped` |

**Rationale**: **No new enum value.** "Partly shipped" as a status would be the obvious name and would break a
rolled-back image, which parses the column into its enum and has no such member - exactly the
expand/contract trap the constitution names. The customer learns "1 of 2 shipped" from the parts,
which a new column carries and an old image ignores.

`orders.TrackingReference` is the part's reference when there is **exactly one** part, else null. An
old reader shows it; a new reader shows the parts.

**Alternatives considered**: a "partly shipped" value on `OrderStatus` - rejected above.

## D5 - Concurrency: the order row is locked first

**Decision**: a part move is `BEGIN; SELECT … FROM orders WHERE "Id" = @id FOR UPDATE; UPDATE
order_shipments … WHERE <guard>; UPDATE orders SET "Status" = <summary> …; COMMIT`.

**Rationale**: Two sellers shipping the last two parts at once would otherwise both compute the summary from a
snapshot in which the other's part is not yet shipped, and the order would stay `Preparing` for
good. Taking the order's row lock first serialises moves **per order** - under read committed each
following statement then sees the other's committed part. Different orders never wait for each other.
A test ships two parts concurrently and asserts `Shipped`
(`Two_parts_shipped_at_the_same_moment_leave_the_order_shipped`), and removing the lock turned it red
in the mutation run.

**Alternatives considered**: no lock, relying on each guarded part update alone - rejected above, the
summary would be computed from a snapshot missing the other part.

## D6 - The seller's address, while it is the job and not after

**Decision**: the sale detail carries the delivery address and phone while the seller's part is
waiting or preparing; not once it is shipped. Never the customer's id or email.

**Rationale**: specs/034 research D4 withheld the address "until the seller has the job of shipping". Now they do,
from the moment the order is paid - and the job ends when the parcel is sent. After that, a seller
holding customers' home addresses for every past sale is exposure with no purpose.

**Alternatives considered**: the address for the life of the sale - rejected above; never - rejected,
the seller cannot ship.

## D7 - The seller's endpoints live under their sales

**Decision**: `POST /api/orders/sales/{orderId}/preparing` and `…/shipment`, `Seller` only.

**Rationale**: The part is addressed by the order id and the token - "my part of this order" - never by a shipment
id or a seller id, so there is nothing to change into somebody else's. Not theirs, not there, not
paid and failed are one 404, `Sale not found.`, as in specs/034.

**Alternatives considered**: addressing the part by a shipment id or a seller id - rejected above, it
hands the caller an id to change into somebody else's.

## D8 - The delivery charge is not split

**Decision**: not split here.

**Rationale**: The customer paid one charge for one delivery option. Dividing it between parts is a decision about
money owed between the shop and its sellers, which is payouts; nothing here pays anybody. Recorded,
not built.

**Alternatives considered**: splitting it now - deferred; specs/037 later split it equally between
the parts, in the currency's smallest unit.
