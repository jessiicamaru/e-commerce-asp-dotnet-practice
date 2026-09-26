# Quickstart: Validating shop names on parcels

> Written on 2026-09-27, after the feature merged (#80), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/api.md](contracts/api.md)

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # or ./start-dev.sh
```

Two approved sellers, A and B, each with a listed and stocked product, and a product of the shop's own
(listed by an administrator). A customer with a delivery address. The pull request used the demo data
(Lan buying from Mai and another seller).

```bash
CUSTOMER=...; TOKEN_A=...; ADDRESS=...
```

Put one of A's, one of B's and one of the shop's products in the customer's cart (`POST /api/cart/items`).

---

## Scenario 1 — The quote names the shops before payment (US1.3)

```bash
curl -s "http://localhost:5000/api/orders/quote?addressId=$ADDRESS&shippingOption=express" \
  -H "Authorization: Bearer $CUSTOMER" | jq '.items[] | {productName, sellerName}'
```

**Expected**: A's line and B's line carry their shop names; the shop's own line has `sellerName: null`.
The quote's `items` are the same `OrderItemResponse` the checkout response returns; valid options are listed by
`GET /api/orders/shipping-options`.

## Scenario 2 — The order and its parcels name the shops (US1.1, US1.2, SC-001)

Check out (`POST /api/orders` with the same address and option), wait for `Paid`, then:

```bash
curl -s http://localhost:5000/api/orders/$ORDER -H "Authorization: Bearer $CUSTOMER" \
  | jq '.items[] | {productName, sellerName}, .shipments[] | {sellerName, isShop, items}'
```

**Expected**: every line names its shop (null for the shop's); three parcels - the shop's first with
`isShop: true, sellerName: null`, then A's and B's with their names. In the storefront the parcels read
"Parcel n of 3 · from …" and "from the shop", and each line "Sold by …".

## Scenario 3 — A rename does not rename the order (US2, SC-002)

```bash
curl -s -X PUT http://localhost:5000/api/sellers/me/shop-name -H "Authorization: Bearer $TOKEN_A" \
  -H 'Content-Type: application/json' -d '{"shopName":"A Renamed"}'
# wait until the CATALOGUE shows the new name - the rename travels by message
until curl -s http://localhost:5000/api/products/$PRODUCT_A | jq -e '.sellerName == "A Renamed"' >/dev/null; do sleep 1; done
curl -s http://localhost:5000/api/orders/$ORDER -H "Authorization: Bearer $CUSTOMER" | jq '[.items[].sellerName]'
```

**Expected**: the order still shows A's **old** name. Waiting for the catalogue first matters: the pull
request's first version of this check could have passed before the rename reached Catalog. Put the name
back afterwards. (`sellerName` on the product is the display read model specs/027 added.)

## Scenario 4 — Nothing else changed (SC-003)

```bash
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expected**: passes; an order of only the shop's goods has one parcel with `isShop: true` and reads as
before.

## Scenario 5 — Frozen in the database

```sql
-- psql on localhost:5434, ecommerce_order_db
SELECT "ProductName", "SellerId", "SellerName" FROM order_items WHERE "OrderId" = '<order id>';
```

**Expected**: `SellerName` set where `SellerId` is set and Catalog knew the name; null where `SellerId`
is null. No row has an empty string.

## Scenario 6 — The automated tests

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests   --filter "FullyQualifiedName~ShopNameTests"
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~VariantSellerPricingTests"
cd ../client && npm test -- src/components/order
```

**Expected**: 5 Order tests (frozen at checkout; a later rename does not change it; each parcel says who
sends it and which is the shop's; the quote names the shop; an absent or empty name on the wire is no
name), the Catalog pricing tests including the three for `seller_name`, and the client's order-lines and
order-shipments tests. At the merge: Catalog 135, Order 103, client 109.
