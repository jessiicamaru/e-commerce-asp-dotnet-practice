# Research: Each seller ships their own part

## D1 - A shipment table, one row per seller per order

**Decision**: `order_shipments (Id, OrderId, SellerId NULL, Status, TrackingReference, UpdatedAt)`,
unique on `(OrderId, SellerId)` with **`NULLS NOT DISTINCT`** (PostgreSQL 15+; the container runs 16).

`SellerId` null is the shop's own part, as it is on `products` and `order_items`. Without
`NULLS NOT DISTINCT` two shop parts for one order would both be allowed, and the "create if missing"
statement below would create a second one every time it ran.

## D2 - One model: the shop's goods are a part too

**Decision**: the shop's own lines form a shipment like any seller's, moved by an administrator.

Two models - order-level fulfilment for the shop, part-level for sellers - would mean every read has
to ask which one an order uses. With one model the administrator's existing endpoints keep their
addresses and simply act on the shop's part. An order holding only the shop's goods has one part, and
behaves exactly as before: `verify-saga.sh` does not change.

An order with **no** shop goods has nothing for the administrator to ship: 409, saying each seller
ships their own. The administrator stays able to see every order; moderating a seller's part is a
different feature.

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

Why both: **a rolled-back image writes orders without parts.** The constitution requires that
redeploying the previous version keeps working, and an older Order places orders in the meantime.
When this version returns, those orders have no parts. A backfill that ran once at migration time
cannot see them; creating on demand can. The part starts in the state the order is in, so an order an
administrator shipped under the old version is a shipped part under the new one.

Reads tolerate a missing part the same way: a sale with no row of its own reads the order's status.

## D4 - The order's own status is a summary, in words the old version knows

**Decision**: `orders.Status` stays, and is recomputed in the same transaction as every part move:

| Parts | orders.Status |
| :-- | :-- |
| none started | unchanged (`Paid`) |
| some started, not all shipped | `Preparing` |
| all shipped | `Shipped` |

**No new enum value.** "Partly shipped" as a status would be the obvious name and would break a
rolled-back image, which parses the column into its enum and has no such member - exactly the
expand/contract trap the constitution names. The customer learns "1 of 2 shipped" from the parts,
which a new column carries and an old image ignores.

`orders.TrackingReference` is the part's reference when there is **exactly one** part, else null. An
old reader shows it; a new reader shows the parts.

## D5 - Concurrency: the order row is locked first

**Decision**: a part move is `BEGIN; SELECT … FROM orders WHERE "Id" = @id FOR UPDATE; UPDATE
order_shipments … WHERE <guard>; UPDATE orders SET "Status" = <summary> …; COMMIT`.

Two sellers shipping the last two parts at once would otherwise both compute the summary from a
snapshot in which the other's part is not yet shipped, and the order would stay `Preparing` for
good. Taking the order's row lock first serialises moves **per order** - under read committed each
following statement then sees the other's committed part. Different orders never wait for each other.
A test ships two parts concurrently and asserts `Shipped`.

## D6 - The seller's address, while it is the job and not after

**Decision**: the sale detail carries the delivery address and phone while the seller's part is
waiting or preparing; not once it is shipped. Never the customer's id or email.

specs/034 research D4 withheld the address "until the seller has the job of shipping". Now they do,
from the moment the order is paid - and the job ends when the parcel is sent. After that, a seller
holding customers' home addresses for every past sale is exposure with no purpose.

## D7 - The seller's endpoints live under their sales

**Decision**: `POST /api/orders/sales/{orderId}/preparing` and `…/shipment`, `Seller` only.

The part is addressed by the order id and the token - "my part of this order" - never by a shipment
id or a seller id, so there is nothing to change into somebody else's. Not theirs, not there, not
paid and failed are one 404, `Sale not found.`, as in specs/034.

## D8 - The delivery charge is not split

The customer paid one charge for one delivery option. Dividing it between parts is a decision about
money owed between the shop and its sellers, which is payouts; nothing here pays anybody. Recorded,
not built.
