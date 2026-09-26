# Quickstart: Validating ratings and reviews

> Written on 2026-09-27, after the feature merged (#98), from the code at that merge, the pull request and docs/features/ratings-and-reviews.md.

**Feature**: [spec.md](./spec.md) | **Contracts**: [http-api.md](./contracts/http-api.md), [messages.md](./contracts/messages.md)

How to show the feature works. Each scenario names the requirement or success criterion it proves. The results
recorded for #98 are quoted where the pull request gives them; which of the manual curl scenarios below were run
by hand at the merge is not recorded - the PR's evidence is the automated suites, Bruno and `verify-saga.sh`.

---

## Prerequisites

```bash
cd server
docker compose up -d                                  # Postgres (Catalog 5433, Order 5434), RabbitMQ, Mailpit ...
./start-dev.sh                                        # or ./start-dev.ps1 - migrations, then every service and the gateway
```

The Catalog migration `20260923212320_AddProductReviews` must be applied (start-dev does it). Everything below
goes through the gateway on `:5000`.

```bash
login() {
  curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
    -d '{"email":"'"$1"'","password":"'"$2"'"}' | jq -r .token
}
ADMIN=$(login "$ADMIN_EMAIL" "$ADMIN_PASSWORD")
CUSTOMER=$(login "$CUSTOMER_EMAIL" "$CUSTOMER_PASSWORD")     # a customer with a paid order ($ORDER) for $PRODUCT
```

A customer becomes eligible only by receiving a parcel, which needs a paid order whose parcel has shipped
(specs/035, specs/040). The Bruno collection builds exactly that state before its `reviews` folder runs; by hand,
place and pay an order for `$PRODUCT`, ship the shop's parcel as admin
(`POST /api/orders/{id}/preparing`, then `POST /api/orders/{id}/shipment` with a tracking reference - the
requests Bruno's `admin-audit/prepare order` and `ship order` send), and read the parcel's id (`$PARCEL`) from
`GET /api/orders/$ORDER`.

---

## Scenario 1 - Nobody reviews what they have not received (FR-005, SC-001)

```bash
curl -s -X PUT http://localhost:5000/api/products/$PRODUCT/reviews/mine \
  -H "Authorization: Bearer $CUSTOMER" -H 'Content-Type: application/json' -d '{"rating":5}'
```

**Expected** (before the parcel is confirmed): `403`, `"detail": "Only a customer who has received this product
can review it."`. `GET .../reviews/mine` answers `{"eligible": false, "review": null}`.

Without a token the same `PUT` is `401` (Bruno: `security-checks/reviewing without a token is 401`).

---

## Scenario 2 - Receiving the parcel grants the right (FR-001, FR-014)

```bash
curl -s -X POST http://localhost:5000/api/orders/$ORDER/shipments/$PARCEL/received -H "Authorization: Bearer $CUSTOMER"
sleep 2
curl -s http://localhost:5000/api/products/$PRODUCT/reviews/mine -H "Authorization: Bearer $CUSTOMER"
```

**Expected**: `{"eligible": true, "review": null}` once the event has crossed the broker (seconds). In Catalog's
database:

```bash
docker exec -it <catalog-postgres-container> psql -U "$DB_USER" -d ecommerce_catalog_db -c \
  "SELECT * FROM review_eligibility WHERE \"ProductId\" = '$PRODUCT';"
```

one row for the customer. Confirm the parcel again: `200`, and still one row (the second confirmation changes
nothing and announces nothing).

---

## Scenario 3 - Write, then edit the same review (US1, FR-002, FR-004, FR-006)

```bash
curl -s -X PUT http://localhost:5000/api/products/$PRODUCT/reviews/mine -H "Authorization: Bearer $CUSTOMER" \
  -H 'Content-Type: application/json' -d '{"rating":5,"body":"Sharp and quiet"}'
curl -s -X PUT http://localhost:5000/api/products/$PRODUCT/reviews/mine -H "Authorization: Bearer $CUSTOMER" \
  -H 'Content-Type: application/json' -d '{"rating":4,"body":"Sharp, a little loud","authorName":"Somebody else"}'
```

**Expected**: both `200` with the **same** `id`; the second has `rating` 4. `authorName` is the customer's first
name from the token both times - the `authorName` in the body is ignored. A `rating` of 0 or 6 is `400`.

---

## Scenario 4 - Shoppers read it, and the product carries the average (US2, FR-003, FR-015, SC-003)

```bash
curl -s "http://localhost:5000/api/products/$PRODUCT/reviews?pageSize=50"       # no token
curl -s "http://localhost:5000/api/products/$PRODUCT" | jq '{ratingAverage, ratingCount}'
```

**Expected**: the review is listed; the product's `ratingAverage` and `ratingCount` equal the average and count
of the visible reviews. Check it against the rows:

```sql
SELECT p."RatingAverage", p."RatingCount",
       round(avg(r."Rating")::numeric, 2) AS computed_avg, count(r."Id") AS computed_count
FROM products p LEFT JOIN product_reviews r ON r."ProductId" = p."Id" AND r."HiddenAt" IS NULL
WHERE p."Id" = '<product id>'
GROUP BY p."Id";
```

The two pairs must agree for every product.

---

## Scenario 5 - Hide and restore (US3, FR-010, FR-011, FR-012)

```bash
REVIEW=<id from scenario 3>
curl -s -X POST http://localhost:5000/api/reviews/$REVIEW/hide -H "Authorization: Bearer $CUSTOMER" \
  -H 'Content-Type: application/json' -d '{"reason":"x"}'                                     # 403
curl -s -X POST http://localhost:5000/api/reviews/$REVIEW/hide -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"reason":"Advertising"}'                           # 200
curl -s -X POST http://localhost:5000/api/reviews/$REVIEW/hide -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"reason":"Again"}'                                 # 409
curl -s "http://localhost:5000/api/reviews?hidden=true&pageSize=50" -H "Authorization: Bearer $ADMIN"
curl -s -X POST http://localhost:5000/api/reviews/$REVIEW/restore -H "Authorization: Bearer $ADMIN"  # 200
```

**Expected**: while hidden, the review is absent from `GET /api/products/$PRODUCT/reviews`, the product's average
and count leave it out, and the staff hidden list shows it with `productName` and `hiddenReason`. A blank reason
is `400`. After restore it is back in both. `GET /api/audit` (Admin) shows `ReviewHidden` and `ReviewRestored`
under `Moderation`.

---

## Scenario 6 - The seller is told once (US4, FR-013, SC-005)

Review a **seller's** product (scenario 3 on a product with a `sellerId`), then edit it. Signed in as that seller:

```bash
curl -s "http://localhost:5000/api/notifications" -H "Authorization: Bearer $SELLER"
```

**Expected**: one `NewReview` notice with `product` and the first `rating`; none for the edit. A shop product
(no seller) notifies nobody.

---

## Scenario 7 - Automated suites (SC-002, SC-003, SC-004)

```bash
cd server
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~ReviewTests"
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Order.Tests \
  --filter "FullyQualifiedName~DeliveryTests.A_confirmed_parcel_announces_the_products_in_it_once|FullyQualifiedName~DeliveryTests.The_sweep_announces_each_parcel_it_delivers"
cd ../client && npm test -- product-reviews admin-reviews
```

**Expected**: the five `ReviewTests` pass (not received → 403; one each signed with the first name, average
3.00 from 2; hidden neither shown nor counted, 409 on a second hide, restored back to 3.00 from 2, `ReviewHidden`
audited under Moderation; seller told once; receiving twice is one row), and the two `DeliveryTests` pass (a
confirmed parcel announces only its own products, once; the sweep announces each of two parcels once, and a
second sweep nothing). **Recorded for #98**: Order 176/176, Catalog 149/149, Identity 71/71, client 221/221 with
lint, type-check and build clean.

**Mutation checks recorded in #98**, each red and then restored: counting hidden reviews in the recompute breaks
`A_hidden_review_is_neither_shown_nor_counted`; skipping the eligibility check breaks
`Somebody_who_has_not_received_it_cannot_review_it`. To repeat the first, remove `AND "HiddenAt" IS NULL` from
both sub-selects in `ReviewRepository.SaveAndRecomputeAsync`; for the second, remove the `IsEligibleAsync` check
in `ReviewHandlers.Handle(WriteReviewCommand)`. Restore the file afterwards and touch it before re-running, or
MSBuild may test the mutated build.

---

## Scenario 8 - Bruno, end to end (SC-001)

```bash
cd bruno
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

Run the whole collection, not the folder alone: `reviews` (seq 14) relies on `customerToken`, `productId` and the
parcel the `admin-audit` folder confirms. **Expected**: the `reviews` folder's ten requests - eligible, review
signed "Bruno", the second write edits the same review, listed, the product carries its average, a customer
cannot hide (403), a moderator hides it, gone from the page, among the staff's hidden, restored - plus
`seller/someone who did not receive it cannot review it` (403 with the sentence) and
`security-checks/reviewing without a token is 401`. **Recorded for #98**: 180/180 requests, 290 tests.

---

## Scenario 9 - The saga is unaffected

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expected**: passes - the delivery change sits after the saga's end. **Recorded for #98**: passes.
