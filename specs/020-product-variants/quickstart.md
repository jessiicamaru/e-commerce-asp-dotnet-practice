# Quickstart: Product Variants

## Tests

```bash
cd server
DB_PASSWORD=... dotnet test        # Catalog, Inventory, Cart, Order
```

New: `Ecommerce.Catalog.Tests/VariantTests.cs`, `Ecommerce.Cart.Tests/VariantLineTests.cs`,
`Ecommerce.Order.Tests/VariantCheckoutTests.cs`, plus the migration backfill test in Catalog.

## End to end

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh    # must pass unchanged
cd ../bruno && npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

## By hand

```bash
P=$(curl -s -X POST -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"name":"Camera","price":1000,"sku":"CAM-1","categoryId":"'$CAT'"}' \
  http://localhost:5000/api/products | jq -r .id)

# a second shape of the same product
curl -s -X POST -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"sku":"CAM-1-KIT","price":1400,"options":[{"name":"Kit","value":"With 24-105mm"}]}' \
  http://localhost:5000/api/products/$P/variants

curl -s http://localhost:5000/api/products/$P | jq '.variants[] | {sku, price, optionSummary}'
curl -s "http://localhost:5000/api/products?pageSize=1" | jq '.items[0] | {price, priceVaries, variantCount}'
```

Then: put stock on one variant only, add that variant to the cart, check out, and read the order line —
it carries the SKU and the option summary, and the total uses that variant's price.

## The upgrade itself

The point of FR-009/FR-010. Against a database that already has products, stock, carts and orders:

```bash
# before: note a product's price and a cart line
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build catalog cart order inventory
# after: the same product still prices the same, the cart line still resolves, an order still places
```
