# Quickstart: A seller can see what they sold

Against a started stack (`start-dev.sh` or the container overlay), with Catalog and Order rebuilt.

## 1. Tests

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests
cd ../client && npm test
```

## 2. A sale, end to end

1. Register a seller (`POST /api/auth/register-seller`), list a product, set a price, set stock.
2. As a customer: add the seller's product **and** a shop product to the cart, save an address, check out.
3. Wait for the order to reach `Paid`.
4. As the seller: `GET /api/orders/sales` - one row, `lineCount: 1`, `subtotal` equal to that line
   alone, not the order's total.
5. `GET /api/orders/sales/{orderId}` - one item; no `shippingAddress`, `userId` or `totalAmount`.
6. As a second seller: the same order id is `404 "Sale not found."`, and the list does not have it.
7. As the customer: `GET /api/orders/sales` is `403`.

## 3. Nothing else moved

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

and the Bruno collection, headless.

## 4. The storefront

Sign in as the seller, open **My shop → Sales**, open the order, switch language and currency: the
amounts stay in the order's currency.
