# Quickstart: Validating seller stock

> Written on 2026-09-27, after the feature merged (#71), from the code at that merge, the pull request and
> docs/features/marketplace.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/api.md](contracts/api.md)

Each scenario maps to a success criterion or an acceptance scenario. Everything goes through the gateway
on `:5000`; a hidden button in the storefront is not a refusal, so the refusals are checked against the
API.

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # or ./start-dev.sh
```

Inventory needs Catalog's gRPC port: `catalog:8081` in compose, `localhost:5157` under `start-dev`
(`CATALOG_GRPC_ADDRESS` overrides it).

You need **two seller accounts, each with an approved product**, and a customer. Since this feature
merged, becoming a seller got longer: an application is approved by staff (specs/044), the address must
be confirmed first (specs/063), and a seller's product waits for review before it is on sale (specs/045).
The quickest route is to run Bruno's `seller` folder twice (it registers a seller, reads the
confirmation link from Mailpit, approves the shop and the product) and note `sellerToken` and
`sellerProductId` from each run. Then:

```bash
TOKEN_A=...      # seller A's access token
VARIANT_A=...    # seller A's product id - also its first variant's id (specs/020)
TOKEN_B=...      # seller B's access token
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

---

## Scenario 1 — A seller stocks her own product (US1, SC-001)

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X PUT http://localhost:5000/api/stock/$VARIANT_A \
  -H "Authorization: Bearer $TOKEN_A" -H 'Content-Type: application/json' -d '{"quantityOnHand":3}'
curl -s http://localhost:5000/api/stock/$VARIANT_A | jq
```

**Expected**: `200`, then `quantityOnHand: 3`, `quantityReserved: 0`, `quantityAvailable: 3`. A few seconds
later `GET /api/products/$VARIANT_A` reads `availability: "InStock"` (the announcement, unchanged).

If the first call answers `404` with `"… is not registered in inventory."`, the stock row has not arrived
from the broker yet (US4) — retry. That is the one 404 worth retrying.

## Scenario 2 — Another seller is refused with 404, never 403 (US2, SC-002)

```bash
curl -s -X PUT http://localhost:5000/api/stock/$VARIANT_A \
  -H "Authorization: Bearer $TOKEN_B" -H 'Content-Type: application/json' -d '{"quantityOnHand":0}' | jq
curl -s -X PUT http://localhost:5000/api/stock/$(uuidgen) \
  -H "Authorization: Bearer $TOKEN_B" -H 'Content-Type: application/json' -d '{"quantityOnHand":0}' | jq
curl -s http://localhost:5000/api/stock/$VARIANT_A | jq .quantityOnHand
```

**Expected**: both refusals are `404` with the **same** `detail` shape, `Product with ID '…' was not found.`;
seller A's stock still reads `3`.

## Scenario 3 — The administrator and the customer (US1.4, edge cases)

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X PUT http://localhost:5000/api/stock/$VARIANT_A \
  -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d '{"quantityOnHand":5}'
```

**Expected**: `200`. With a customer's token the same call is `403`; with none, `401`. Bruno's
`seller/a customer cannot stock it` asserts the 403.

## Scenario 4 — A shopper buys it and the stock moves by exactly one (SC-001, SC-003)

As a customer, add one unit of `$VARIANT_A` to the cart and check out (`POST /api/cart/items`, then
`POST /api/orders` with an address and a delivery option). When the order is `Paid`:

**Expected**: `GET /api/stock/$VARIANT_A` reads on hand one lower, `quantityReserved: 0`. Then run the
saga check, which exercises the service this feature touched:

```bash
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-auth.sh
```

## Scenario 5 — The automated tests (Principle V)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Inventory.Tests --filter "FullyQualifiedName~SellerStockTests"
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests   --filter "FullyQualifiedName~VariantOwnershipTests"
cd ../client && npm test -- src/pages/shop-product
```

**Expected**: 7 Inventory tests (owner passes; another's is 404 and never 403; the shop's own is refused;
an administrator still stocks anything; an administrator costs no call to Catalog; a missing row says
something different; Catalog unreachable is not a refusal), 4 Catalog tests, and the 5 stock tests of
`SellerProductPage` pass. At the merge the suites stood at Inventory 38, Catalog 115, client 38.

## Scenario 6 — The page (US3)

Sign in as seller A in the storefront and open `/shop/products/$VARIANT_A`.

**Expected**: the quantity sits beside the price editor, prefilled with **on hand** (not available), with
the number held for orders shown beside it; a refusal is shown in the server's own words.
