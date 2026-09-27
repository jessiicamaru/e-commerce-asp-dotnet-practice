# Quickstart: Validating the shop page

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/)

## Scenario 1: The tests (SC-001, SC-002, SC-003)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~ShopPageTests"
DB_PASSWORD=... dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~ShopDescriptionTests"
cd ../client
npx vitest run src/pages/shop-front src/pages/product src/components/seller/describe-shop-dialog
```

**Expected**: all green. Catalog has 4 tests, Identity has 3, and the client has 3 for the shop page, 2 for the product
page's shop link and 1 for the dialog.

## Scenario 2: Through the gateway (SC-004)

Bruno's `seller/` requests at seq 83-85 and `security-checks/` at seq 61-62 pass. To check by hand:

```bash
curl -fsS -X PUT http://localhost:5000/api/sellers/me/description -H "Authorization: Bearer $SELLER" \
  -H 'Content-Type: application/json' -d '{"description":"Used Fujifilm bodies."}'
sleep 2
curl -fsS http://localhost:5000/api/shops/$SELLER_ID | jq
curl -fsS "http://localhost:5000/api/products?sellerId=$SELLER_ID" | jq '.items[] | {name, sellerId}'
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/shops/00000000-0000-0000-0000-000000000001
```

**Expected**:

- The shop comes back with its name, the description and `productCount`.
- Every listed product carries that `sellerId`.
- The last command prints `404`.

## Scenario 3: In the storefront (SC-003)

1. Open a seller's product at `http://localhost:8088`. The shop name under the product's name is a link.
2. Follow the link to `/shops/{id}`. The page shows the shop's name, its description, "N products on sale", and only that
   shop's products.
3. As the seller, go to `/shop`, choose **Description**, save some words, and reload the shop page. The words are shown.
4. A product of the shop itself says "Sold by The shop" with no link.

## Scenario 4: Mutations (SC-005)

Apply each change below on its own, run the suite named beside it, and expect it to go red. Then restore the file.

| Mutation | Suite |
| :-- | :-- |
| `GetProductsQueryHandler` ignores `SellerId` | `ShopPageTests` |
| `GetShopQuery` serves a suspended seller | `ShopPageTests` |
| The `DescriptionObservedAt` guard removed from the upsert | `ShopPageTests` |
| The shop page asks for products without `sellerId` | `pages/shop-front` |
| The product page links the shop's own goods | `pages/product` |
| The dialog sends the text untrimmed | `describe-shop-dialog` |
