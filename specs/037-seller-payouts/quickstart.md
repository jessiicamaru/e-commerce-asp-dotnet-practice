# Quickstart: What the shop owes each seller

With the stack running and the demo accounts from `local/demo-accounts.md`:

1. As a customer, check out a cart holding goods from two sellers with standard delivery (30,000 ₫).
2. As each seller, `GET /api/orders/sales/{id}`: `shippingShare` is 15,000 each (or 10,000 each when
   the shop's own goods are also on the order), `commission` is 10% of `goodsTotal`.
3. `GET /api/orders/sales/balance`: the amount is **on the way**.
4. Each seller ships their part; the amount moves to **due**.
5. As an administrator, `GET /api/orders/payouts/due` lists both; `POST /api/orders/payouts` for one
   seller → 201; the same again → 409.
6. The seller's balance shows it **paid out**, and `/sales/payouts` lists the payout.
7. Change `Marketplace:CommissionRate`, restart Order, re-read step 2: unchanged.
