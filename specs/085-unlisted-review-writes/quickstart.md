# Quickstart: Validating no reviews off the shelf

> Written on 2026-09-27, after the feature merged (#177), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

---

## Scenario 1 - The test (SC-001, SC-002, SC-004)

```bash
cd server
DB_PASSWORD=<your password> SEAWEEDFS_ACCESS_KEY=... SEAWEEDFS_SECRET_KEY=... dotnet test tests/Ecommerce.Catalog.Tests \
  --filter "FullyQualifiedName~Off_the_shelf_nobody_writes_a_review_and_its_rating_does_not_move"
```

**Expected**: green. It records eligibility for a buyer, has them review at 5, takes the product down as a
moderator, then asserts: a second write throws `NotFoundException` with `Product not found.`; `reviews/mine` says
not eligible; the rating is still (5, 1). Put back to `Approved`, a review of 4 is stored and the rating is (4, 1).
The pull request records Catalog 206/206, and that removing the write gate, or the eligibility gate, turns this test
red.

## Scenario 2 - Bruno (SC-003)

Run the collection (see `CLAUDE.md`). In the `seller/` folder, right after `a moderator takes the product down`
(seq 78) and the reads of specs/081, `a review of it is a 404` (seq 82) sends
`PUT /api/products/{{sellerProductId}}/reviews/mine` as `customerToken` - a customer who never received the product -
and expects `404`. Before this change it was `403`. Recorded at the merge: **268/268 requests, 438/438 tests**.

## Scenario 3 - By hand

With `P` a product the customer `C` received and reviewed, and `$ADMIN` an administrator's token:

```bash
curl -fsS -X POST http://localhost:5000/api/products/$P/take-down -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"reason":"quickstart 085"}'
curl -s -o /dev/null -w '%{http_code}\n' -X PUT http://localhost:5000/api/products/$P/reviews/mine \
  -H "Authorization: Bearer $C" -H 'Content-Type: application/json' -d '{"rating":1,"body":"off the shelf"}'   # 404
curl -fsS http://localhost:5000/api/products/$P/reviews/mine -H "Authorization: Bearer $C" | jq .eligible          # false
```

```sql
-- ecommerce_catalog_db on localhost:5433: unchanged by the refused write
SELECT "RatingAverage", "RatingCount" FROM products WHERE "Id" = '<P>';
```

Put it back on sale with `POST /api/products/$P/resubmit` (its seller or an administrator) then
`POST /api/products/$P/approve` (staff), and the same `PUT` answers `200`.
