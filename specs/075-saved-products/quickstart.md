# Quickstart: Validating saved products

> Written on 2026-09-27, after the feature merged (#159), from the code at that merge, the pull request and docs/features/saved-products.md.

**Feature**: [spec.md](./spec.md) | **Contracts**: [http-api.md](./contracts/http-api.md), [messages.md](./contracts/messages.md)

How to prove the feature works. Each scenario names the story, requirement or success criterion it checks. These
are the steps; this backfill did not run them. What was run at the merge is in PR #159 and listed at the end.

---

## Prerequisites

```bash
cd server
docker compose up -d                  # Postgres for every service, RabbitMQ, Mailpit, SeaweedFS ...
./start-dev.sh                        # or start-dev.ps1: migrations (including AddSavedProducts) and every service
```

Catalog (5057), Inventory (5060), Identity (5056) and Activity (5063) must be up for scenario 5; the others need
Catalog and Identity only. Everything goes through the gateway on `:5000`.

Tokens: an administrator (seeded from `ADMIN_EMAIL` / `ADMIN_PASSWORD`) and two customers.

```bash
BASE=http://localhost:5000
login() { curl -fsS -X POST $BASE/api/auth/login -H 'Content-Type: application/json' \
  -d "{\"email\":\"$1\",\"password\":\"$2\"}" | jq -r .token; }

ADMIN=$(login "$ADMIN_EMAIL" "$ADMIN_PASSWORD")
for who in mai bao; do
  curl -fsS -X POST $BASE/api/auth/register -H 'Content-Type: application/json' \
    -d "{\"email\":\"$who-$RANDOM@example.test\",\"password\":\"Passw0rd!\",\"firstName\":\"$who\",\"lastName\":\"Test\"}" >/dev/null
done
MAI=$(login <mai's email> 'Passw0rd!')   # use the addresses just registered
BAO=$(login <bao's email> 'Passw0rd!')
```

A product listed by an administrator is approved at once (specs/045), and new products have no stock
(`QuantityOnHand = 0`), which is what scenario 5 needs:

```bash
CAT=<an existing category id>            # GET $BASE/api/categories
SKU=SAVE-$RANDOM
PID=$(curl -fsS -X POST $BASE/api/products -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d "{\"name\":\"Saved check $SKU\",\"description\":\"quickstart\",\"price\":1000000,\"sku\":\"$SKU\",\"categoryId\":\"$CAT\"}" \
  | jq -r .id)
```

---

## Scenario 1 - Save, save again, unsave (US1, FR-001, FR-002, SC-001)

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X PUT    $BASE/api/products/$PID/saved -H "Authorization: Bearer $MAI"  # 204
curl -s -o /dev/null -w '%{http_code}\n' -X PUT    $BASE/api/products/$PID/saved -H "Authorization: Bearer $MAI"  # 204
curl -s $BASE/api/products/saved/ids -H "Authorization: Bearer $MAI" | jq --arg p $PID '[.[] | select(. == $p)] | length'  # 1
curl -s -o /dev/null -w '%{http_code}\n' -X DELETE $BASE/api/products/$PID/saved -H "Authorization: Bearer $MAI"  # 204
curl -s -o /dev/null -w '%{http_code}\n' -X DELETE $BASE/api/products/$PID/saved -H "Authorization: Bearer $MAI"  # 204 again
```

**Expected**: every write is 204, and the ids hold the product exactly once after two saves.

SQL, against `ecommerce_catalog_db` (host port 5433), after saving once more:

```sql
SELECT "CustomerId", "ProductId", "SavedAt" FROM saved_products WHERE "ProductId" = '<PID>';
-- one row per shopper who saved it; SavedAt does not move on a repeat save
```

## Scenario 2 - A product not on sale cannot be saved (US1, FR-006)

Register a seller (or use Bruno's seller folder), list a product as that seller - it waits for review - and save it
as a customer:

```bash
curl -s -w '\n%{http_code}\n' -X PUT $BASE/api/products/<pending product id>/saved -H "Authorization: Bearer $MAI"
curl -s -w '\n%{http_code}\n' -X PUT $BASE/api/products/$(uuidgen)/saved          -H "Authorization: Bearer $MAI"
```

**Expected**: both are 404 with `detail` `Product not found.` - identical, so the pending product's id is not
confirmed.

## Scenario 3 - The list is the caller's own, newest first, read now (US2, FR-002, FR-007)

Save two products as Mai, a moment apart, and one as Bao. Then:

```bash
curl -s "$BASE/api/products/saved?page=1&pageSize=12" -H "Authorization: Bearer $MAI" -H 'Accept-Language: en' \
  | jq '{totalCount, first: .items[0].product.id, keys: (.items[0] | keys)}'
curl -s "$BASE/api/products/saved?page=1&pageSize=12&currency=USD" -H "Authorization: Bearer $MAI" | jq '.items[].product.price'
curl -s "$BASE/api/products/saved?pageSize=51" -H "Authorization: Bearer $MAI" -o /dev/null -w '%{http_code}\n'   # 400
curl -s "$BASE/api/products/saved" -o /dev/null -w '%{http_code}\n'                                                # 401
```

**Expected**: Mai sees only her two, the later one first; each item has exactly `available`, `product` and
`savedAt`; the product reads in the requested language, and a product not priced in dollars has a null price in
the dollar request. `pageSize=51` is 400; no token is 401.

## Scenario 4 - Taken down stays, deleted goes (US2, FR-008, SC-003)

With Mai holding two saved products, take one down as staff (`POST $BASE/api/products/{id}/take-down` with a
reason, specs/045) and delete the other (`DELETE $BASE/api/products/{id}`, Admin). Read Mai's list again.

**Expected**: the taken-down product is still there with `"available": false`; the deleted one is gone
(`totalCount` dropped by one), with no error.

## Scenario 5 - Back in stock, once per flip (US3, FR-003, FR-009, SC-004)

Mai and Bao both save `$PID` (no stock yet). Then stock it through Inventory:

```bash
curl -s -X PUT $BASE/api/stock/$PID -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"quantityOnHand": 5}' >/dev/null
sleep 3
curl -s "$BASE/api/notifications?page=1&pageSize=50" -H "Authorization: Bearer $MAI" \
  | jq --arg l "/products/$PID" '[.items[] | select(.kind == "SavedBackInStock" and .link == $l)] | length'   # 1
```

Set the stock to 7 (still in stock): the count stays 1. Set it to 0 and then to 3: the count becomes 2. Bao's inbox
shows the same. (The first variant of a new product reuses the product's id, so `PUT /api/stock/$PID` stocks it.)

**Expected**: one notice per saver per false-to-true flip, linking to `/products/{id}`, with `data.product` the
product's name. A product taken down before it is stocked tells nobody.

This is the live check PR #159 reports: "a customer saved a new (out of stock) product, an administrator stocked it,
and the customer's notifications held `SavedBackInStock` with a link to it."

## Scenario 6 - The automated tests

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~SavedProductTests"
```

**Expected**: 7 passing - `Saving_twice_or_twenty_times_at_once_keeps_one_entry`,
`Unsaving_removes_it_and_unsaving_what_was_never_saved_is_no_error`, `Only_a_product_on_sale_can_be_saved`,
`The_list_is_the_callers_own_newest_first_in_the_listings_words`,
`A_product_taken_down_since_stays_and_reads_as_unavailable_and_a_deleted_one_goes`,
`Coming_back_in_stock_tells_whoever_saved_it_once_per_flip`,
`A_product_off_the_shelf_coming_back_in_stock_tells_nobody`. They need PostgreSQL on 5433 (and S3 on 8333 for
the fixture): the key's `ON CONFLICT` is the database's guarantee, so it is tested against the database.

```bash
cd client
npm test -- src/components/product/save-button src/pages/saved src/services/saved-product
```

**Expected**: the heart saves and unsaves and sends a signed-out visitor to sign in without asking the server;
the page lists what was saved and marks what can no longer be bought; the service's URLs name no shopper.

## Scenario 7 - Bruno

```bash
cd bruno
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: `bruno/product/` 61-66 green - save 204, save again 204, the ids hold it once, the list's shape,
unsave 204, 401 without a token.

## Scenario 8 - The storefront

Open the storefront, sign in, tap the heart on a product card: it fills (`aria-pressed="true"`). Open **Saved** in
the account menu: `/saved` lists it. Signed out, the heart sends you to sign in and back.

---

## What was run at the merge (PR #159)

- `Ecommerce.Catalog.Tests`: 176/176, of which 7 new in `SavedProductTests`.
- Storefront: 407/407 in 70 files; lint and `tsc` clean.
- Bruno, through the storefront container: 238/238 requests, 387/387 tests.
- The live back-in-stock check of scenario 5 (once, one customer).
- Four mutation checks, each turning `SavedProductTests` red (see [tasks.md](./tasks.md)).

Scenarios 2, 3 and 4 as curl sequences, and scenario 8 by hand: not recorded as run separately; Bruno and the
tests cover their assertions.
