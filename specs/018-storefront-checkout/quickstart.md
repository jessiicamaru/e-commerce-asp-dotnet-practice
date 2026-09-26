# Quickstart: Checkout and Order History

> Written on 2026-09-27, after the feature merged (#48), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

## Prerequisites

```bash
cd server && docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
cd ../client && npm ci && npm run dev          # http://localhost:5173
```

A customer with a cart holding something and one address (specs/017's quickstart does both).

## Scenario 1 - The quote equals the order (SC-001, FR-008, FR-009)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~CheckoutQuoteTests"
```

**Expected**: 5 pass - the quote equals the placed order part by part and equals the stored
`TotalAmount`; a quote places and publishes nothing; an empty cart and an unknown option (`""`,
`teleport`) are refused as checkout refuses them. The pull request recorded Order.Tests 46/46 (41 + 5).

**Negative control** (recorded): add `+ 0.01m` to `TotalAmount` in the quote handler →
`The_quote_is_exactly_what_the_same_choices_then_charge [FAIL]`. Restore it.

## Scenario 2 - Bruno (SC-001)

```bash
cd bruno
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: `order/checkout quote` passes "the parts of the quote add up", and `order/checkout` passes
"charged exactly what the quote said". The pull request recorded 54/54 requests, 72 tests.

## Scenario 3 - End to end through the proxy, both outcomes (US1, US2, SC-003, SC-004)

With a customer token `T`, an address id `A` in GB, and a cart:

```bash
P=http://localhost:5173/api; H="Authorization: Bearer $T"
curl -s -o /dev/null -w 'quote, teleport %{http_code}\n' "$P/orders/quote?addressId=$A&shippingOption=teleport" -H "$H"
curl -s "$P/orders/quote?addressId=$A&shippingOption=express" -H "$H" | jq '{subtotal, shippingPrice, taxTotal, taxRate, totalAmount}'
O=$(curl -s -X POST $P/orders -H "$H" -H 'Content-Type: application/json' \
      -d "{\"addressId\":\"$A\",\"shippingOption\":\"express\"}" | jq -r .orderId)
sleep 2
curl -s $P/orders/$O -H "$H" | jq '{status, subtotal, shippingPrice, taxTotal, discountTotal, taxRate, totalAmount}'
curl -s $P/cart -H "$H" | jq '.lines'
```

A quote for a customer with no address (a second, new customer) gives the 409. Then restart Payment
with `PAYMENT_OUTCOME=Reject`, fill the cart again, and repeat the order.

**Expected**, as the pull request recorded it against the containerised stack with Order rebuilt:

```text
quote, no address        -> 409
quote, teleport          -> 400 errors.ShippingOption
quote GB express         -> subtotal 10.0, shipping 15.0, tax 5.0 (20%), total 30.0
order after 2s: Paid     -> stored 10.0 / 15.0 / 5.0 / 0.0 / 0.2 / 30.0   (identical)
list                     -> 1 [Paid]; cart after: []
other customer's order   -> 404

Payment restarted with PAYMENT_OUTCOME=Reject:
order after 2s: Failed   -> "Payment declined by the stub gateway ..."; cart after: still 2 units
```

## Scenario 4 - In the browser (US1-US3)

1. `/cart` → checkout. The default address and the first option are chosen; the breakdown shows
   subtotal, delivery, tax with its rate and total.
2. Switch to express: the breakdown is priced again.
3. Place the order: the page says "We are reserving your items and taking payment…", then "Thank you for
   your order" with the same figures.
4. `/orders`: the order, newest first, "Paid. We will start preparing it soon."
5. With `PAYMENT_OUTCOME=Reject`: "Not placed: the payment was declined. Your cart is unchanged, so you
   can try again." and a link to the cart.
6. Open another customer's order id: "Order not found."

**Not run at the merge**: the pull request states the pages were not clicked through in a real browser.
