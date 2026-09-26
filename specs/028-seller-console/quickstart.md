# Quickstart: A seller can actually sell

> Written on 2026-09-27, after the feature merged (#65), from the code at that merge, the pull request, docs/features/marketplace.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [api.md](./contracts/api.md)

---

## Prerequisites

```bash
cd server
docker compose up -d
./start-dev.sh                                    # every service and the gateway on :5000
export ADMIN_EMAIL=... ADMIN_PASSWORD=...         # on a line of its own - see the Bruno warning in CLAUDE.md

cd ../client
npm ci
npm run dev                                       # http://localhost:5173
```

---

## Scenario 1 - The client's tests (SC-004)

```bash
cd client
npm test                                          # vitest run
```

**Expected**: 33 pass, across `components/auth/require-role`, `components/shared/server-error`,
`hooks/product`, `services/product`, `pages/shop`, `pages/shop-product-new` and `pages/shop-product`. Among
them: `labels the price with the default currency, not the one being browsed in`,
`sends exactly what was typed, and no seller`, `does not turn the server 404 into a permission message`,
`shows the price of a currency the seller is not browsing in`, `does not accept a role that merely exists`.

Any test that forgets to stub a service call fails with "A test tried to reach the network" - that is
`src/test/setup.ts` working, not a flaky test.

Then the rest of the CI `client` job: `npm run lint` and `npm run build`.

---

## Scenario 2 - Roles on the authentication response (FR-001)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~SellerRolesTests"
```

**Expected**: 4 pass - a customer holds `Customer` only; a seller holds `Seller` and `Customer`; signing in
reports the same roles as registering; refreshing keeps them.

Over the wire:

```bash
curl -s -X POST http://localhost:5000/api/auth/register-seller -H 'Content-Type: application/json' \
  -d '{"email":"alice@example.test","password":"Str0ng!Pass","firstName":"Alice","lastName":"A","shopName":"Alice Cameras"}' | jq .roles
# ["Seller","Customer"] (order not guaranteed)
```

Bruno: `auth/login customer` asserts `Customer` and not `Seller`; `seller/register a seller` asserts both
and that they agree with the token's `role` claims; `auth/login admin` now says in words when a 400 means
the credentials never arrived.

---

## Scenario 3 - The seller's pages (User Stories 1-4, SC-001, SC-003)

Sign in as Alice in the storefront.

1. The top bar shows a shop link. `/shop` shows "Alice Cameras" and an empty state saying what to do.
2. `/shop/products/new`: the price field is labelled **VND** even after switching the currency to USD.
   Create a product; it appears on `/shop` and, as an anonymous visitor, on the public catalogue with
   "Alice Cameras" under it. The form says the listing has no stock yet.
3. `/shop/products/:id`: set a USD price; the VND price already set shows in its own box (not empty);
   upload a photograph; withdraw - a confirmation first - and the product leaves the catalogue.
4. Rename the shop on `/shop`; every listing shows the new name.

Sign in as a plain customer: no shop link, and typing `/shop` lands on `/`.

**At the merge**: screenshots of every new page in both languages, plus English-and-dollars.

---

## Scenario 4 - The server refuses on its own (SC-002)

Register a second seller, Bob, and list a product as each. With **Bob's** token, against **Alice's**
listing:

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X PUT    http://localhost:5000/api/products/$ALICE_PRODUCT/variants/$VARIANT/prices/USD -H "Authorization: Bearer $BOB" -H 'Content-Type: application/json' -d '{"amount":10}'
curl -s -o /dev/null -w '%{http_code}\n' -X DELETE http://localhost:5000/api/products/$ALICE_PRODUCT -H "Authorization: Bearer $BOB"
```

**Expected**: `404` every time, never `403`. **At the merge**: Bob was refused all six writes - set a price,
remove a price, translate, add a variant, remove the image, delete - with 404; Alice's listing was untouched
and she could still write to it; `GET /api/products/mine` gave `alice sees 1, bob sees 0`.

The whole run at the merge: 282 backend tests (Identity 50 → 54), 33 client tests, Bruno 91/91 requests and
141/141 tests, `verify-auth.sh` and `verify-saga.sh` passing.
