# Quickstart: A Cart and an Address Book

> Written on 2026-09-27, after the feature merged (#47), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

## Prerequisites

```bash
cd server && docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
cd ../client && npm ci && npm run dev          # http://localhost:5173
```

At least one product with a price. `PRODUCT` below is its id (at this merge a product id; since
specs/020 the cart names a variant).

## Scenario 1 - The calls, through the proxy, as a new customer (FR-001-FR-006, SC-001, SC-003, SC-004)

```bash
P=http://localhost:5173/api; E="cart-$(date +%s)@example.test"
T=$(curl -s -X POST $P/auth/register -H 'Content-Type: application/json' \
      -d "{\"email\":\"$E\",\"password\":\"Passw0rd!23\",\"firstName\":\"C\",\"lastName\":\"Q\"}" | jq -r .token)
H="Authorization: Bearer $T"; J='Content-Type: application/json'

curl -s -o /dev/null -w 'add 2 %{http_code}\n'  -X POST $P/cart/items -H "$H" -H "$J" -d "{\"productId\":\"$PRODUCT\",\"quantity\":2}"
curl -s -o /dev/null -w 'set 3 %{http_code}\n'  -X PUT  $P/cart/items/$PRODUCT -H "$H" -H "$J" -d '{"quantity":3}'
curl -s -o /dev/null -w 'add -1 %{http_code}\n' -X POST $P/cart/items -H "$H" -H "$J" -d "{\"productId\":\"$PRODUCT\",\"quantity\":-1}"
curl -s $P/cart -H "$H" | jq '{lines: [.lines[] | {name, quantity, unitPrice, lineTotal, status}], estimatedTotal}'

# sign out and in again: the cart is still there
curl -s -o /dev/null -X POST $P/auth/logout
T=$(curl -s -X POST $P/auth/login -H "$J" -d "{\"email\":\"$E\",\"password\":\"Passw0rd!23\"}" | jq -r .token)
H="Authorization: Bearer $T"
curl -s $P/cart -H "$H" | jq '[.lines[] | {name, quantity}], .estimatedTotal'

# addresses
A1=$(curl -s -X POST $P/addresses -H "$H" -H "$J" -d '{"recipientName":"C Q","line1":"1 St","city":"Hanoi","postalCode":"100000","country":"vn"}' | jq -r '.id, .country, .isDefault')
echo "$A1"
curl -s -o /dev/null -w 'bad fields %{http_code}\n' -X POST $P/addresses -H "$H" -H "$J" \
  -d '{"recipientName":"C","line1":"1","city":"X","postalCode":"!","country":"ZZ"}'
```

**Expected**, as the pull request recorded it:

```text
add 2 -> 204 · set quantity 3 -> 204 · add -1 -> 400
cart: [Fail Widget, qty 3, 5.00, total 15.00, Available]
logout -> 204, login again -> cart still [(Fail Widget, 3)], estimate 15.0
address "vn" -> saved as VN, default (the first one)
second address, make it default -> 204; list shows only the GB one as default
postal "!" + country "ZZ" -> 400 with errors.PostalCode and errors.Country
edit -> 200 · delete -> 204 · remove line -> 204, cart empty
```

The product names and prices are the pull request's data. (Registration and address rules have grown
since; the body above may need adjusting against today's stack.)

## Scenario 2 - In the browser (US1-US3)

1. Signed out, open a product: "Sign in to add this to your cart". Sign in: back on the product.
2. Add 2: "Added 2 to your cart. View cart".
3. `/cart`: the line, "Estimated ... An estimate from today's prices, before shipping and tax."
4. Change the quantity, remove the line: each change is reflected from the server.
5. Stop Catalog, reload `/cart`: "Prices could not be checked just now. Your items are safe".
6. `/addresses`: add two, make the second default, edit, delete; a bad postal code shows its message
   under the field.

**Not run at the merge**: the pages were type-checked and built and the calls checked with curl; the
pull request says nobody clicked through them.
