# Research: What the shop owes each seller

## D1 - Frozen amounts on the part, not recomputed

**Decision**: `order_shipments` gains `GoodsTotal`, `Commission` and `ShippingShare` (`decimal(18,2) NULL`),
written at checkout with the part; `orders` gains `CommissionRate decimal(5,4) NULL`.

Storing the amounts rather than the rate alone means every read - a sale, a balance, a payout - is a
sum over stored columns, never a second rounding that could land a unit away from the first. It is
the same rule the order's own total follows (specs/012): the parts are stored, and a CHECK proves they
add up. The rate is stored too, so an amount can be explained.

Rejected: computing earnings at read time from the order's rate and lines. It needs the currency's
minor unit at read time (configuration, which can change) and it re-derives a number that was an
agreement at the moment of sale.

## D2 - Equal split, remainder to the first part, in minor units

`share = floor(delivery / n)` in the currency's smallest unit; the remainder goes to the first part,
and parts are ordered **shop first, then by seller id** - deterministic, so the quote and the order
agree. 30,000 ₫ over 3 parts is 10,000 each; $2.00 over 3 is 0.68 / 0.66 / 0.66. A pure function,
`DeliverySplit.Split`, unit-tested on its own.

## D3 - Commission on goods before tax; tax stays with the shop

The shop charged the customer, so the shop owes the tax - tax is not the seller's revenue.
`Commission = round(GoodsTotal × rate)` half away from zero (ADR-002's rounding), per part.
Owed to the seller: `GoodsTotal − Commission + ShippingShare`.

## D4 - Due means shipped

A part is due once its status is `Shipped`. There is no delivery confirmation to wait for; shipped is
the last thing the system knows. Only orders in `Sales.Statuses` (paid onwards) count at all, because
a failed order's parts exist (they are written at checkout) and must never become owed.

## D5 - A payout claims its parts with one guarded UPDATE

```sql
UPDATE order_shipments s SET "PayoutId" = @payout
  FROM orders o
 WHERE o."Id" = s."OrderId" AND s."SellerId" = @seller AND s."Status" = 'Shipped'
   AND s."PayoutId" IS NULL AND s."GoodsTotal" IS NOT NULL
   AND o."Currency" = @currency AND o."Status" IN (<paid statuses>)
```

then the amount is summed over the rows **this** payout claimed, and the `payouts` row is inserted, in
one transaction. Two administrators at once: the second UPDATE waits on the row locks, re-evaluates
`PayoutId IS NULL`, claims nothing and is refused with 409 - no double payment, no lock of our own
needed. Zero rows claimed rolls back, so a refused payout leaves nothing behind.

Rejected: summing first, then claiming. The sum would be of rows another payout may take in between.

## D6 - Older orders are not owed by this feature

Parts without `GoodsTotal` (orders before this; parts created on demand for an older image's order)
are excluded from balances and payouts, and a sale shows its earnings as `null`. Same reason as
specs/034 D7: the terms were not recorded, and inventing them now is not recording them.

## D7 - Where it lives

Order: it owns the orders, the parts and who sold what. `payouts` is Order's table. No event: nothing
else needs to know yet, and a contract with no consumer is the thing CLAUDE.md warns against.
