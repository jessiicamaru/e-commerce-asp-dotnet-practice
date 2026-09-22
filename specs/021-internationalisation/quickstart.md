# Quickstart: Speaking More Than One Language

## Tests

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests tests/Ecommerce.Order.Tests
```

## By hand, through the gateway

```bash
# the same product, twice
curl -s "$GW/api/products/$P?lang=vi" | jq '{language, name}'
curl -s -H 'Accept-Language: en' "$GW/api/products/$P" | jq '{language, name}'

# give it Vietnamese
curl -s -X PUT -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"name":"Máy ảnh Sony A7 IV","description":"Cảm biến full-frame 33MP"}' \
  "$GW/api/products/$P/translations/vi"

# and find it without typing the accents
curl -s "$GW/api/products?searchTerm=may%20anh&lang=vi" | jq '.items[].name'
```

## The storefront

```bash
cd client && npm run dev
```

Switch language in the top bar: every label changes, the product names change with them, and a reload
keeps the choice.

## What to look at in the database

```sql
SELECT p."Name" AS default_text, t."Language", t."Name"
FROM products p LEFT JOIN product_translations t ON t."ProductId" = p."Id"
ORDER BY p."CreatedAt" DESC LIMIT 5;

SELECT o."Language", i."ProductName", i."OptionSummary"
FROM orders o JOIN order_items i ON i."OrderId" = o."Id"
ORDER BY o."CreatedAt" DESC LIMIT 5;   -- the words this order was bought with
```
