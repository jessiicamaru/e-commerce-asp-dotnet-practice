# Quickstart: Validating product review before sale

> Written on 2026-09-27, after the feature merged (#97), from the code at that merge, the pull request and docs/features/catalog.md and docs/features/moderation-and-staff.md.

**Feature**: [spec.md](./spec.md) | **Contracts**: [http-api.md](./contracts/http-api.md), [messages.md](./contracts/messages.md), [grpc.md](./contracts/grpc.md)

How to show the feature working. Each scenario names the requirement or success criterion it proves.
What was actually run at merge is at the end.

---

## Prerequisites

```bash
cd server
docker compose up -d                         # Postgres containers, RabbitMQ, Mailpit ...
./start-dev.sh                               # or ./start-dev.ps1 - migrations, every service, the gateway on :5000
```

The Catalog migration `20260923210256_AddProductReview` is applied by `start-dev`; on its own:

```bash
dotnet ef database update --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ \
                          --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/
```

Tokens (the seeded administrator, a customer, and a seller whose shop an administrator has approved -
specs/044; Bruno's `seller/` folder does exactly this in requests 1 to 8):

```bash
GW=http://localhost:5000
ADMIN=$(curl -fsS -X POST $GW/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
SELLER=...     # sign in again after the shop is approved, so the token carries Seller
CUSTOMER=...   # any registered customer
CATEGORY=$(curl -fsS "$GW/api/categories" | jq -r '.[0].id')
```

---

## Scenario 1 - Existing products stayed approved (FR-003, SC-005)

Against `ecommerce_catalog_db` (host port 5433):

```sql
SELECT "ReviewStatus", count(*) FROM products GROUP BY 1;
-- before any seller lists anything after the migration: one row, Approved
SELECT column_name, data_type, character_maximum_length, is_nullable, column_default
FROM information_schema.columns
WHERE table_name = 'products' AND column_name IN ('ReviewStatus','ReviewReason','SubmittedAt','ReviewedAt','ReviewedBy');
-- ReviewStatus: character varying(20), NO, 'Approved'::character varying
```

**Expected**: every pre-existing row reads `Approved`; `IX_products_ReviewStatus_SubmittedAt` exists.

---

## Scenario 2 - A seller's product is hidden and unsellable (US1, FR-005, FR-006, SC-003)

```bash
PID=$(curl -fsS -X POST $GW/api/products -H "Authorization: Bearer $SELLER" -H 'Content-Type: application/json' \
  -d '{"name":"Review demo","price":40000000,"sku":"REVDEMO1","categoryId":"'"$CATEGORY"'"}' | jq -r .id)

curl -s -o /dev/null -w '%{http_code}\n' $GW/api/products/$PID                                # 404
curl -fsS "$GW/api/products?searchTerm=Review%20demo" | jq '[.items[].id] | index("'"$PID"'")'  # null
curl -fsS $GW/api/products/$PID -H "Authorization: Bearer $SELLER" | jq .reviewStatus           # "Pending"
curl -fsS "$GW/api/products/mine" -H "Authorization: Bearer $SELLER" | jq '[.items[] | select(.id=="'"$PID"'") | .reviewStatus]'
```

**Expected**: 404 and absent to a shopper; `Pending` to its seller and in their own list. The
"not sellable" half is asserted in the test suite (Scenario 8), because checkout needs a stocked,
carted product: put it in a customer's cart and `POST /api/orders` answers 409
`Not currently for sale: Review demo.`

As an administrator, create a product the same way: `reviewStatus` is `Approved` at once (FR-005).

---

## Scenario 3 - Only staff decide (FR-007, US2 scenario 6, SC-006)

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST $GW/api/products/$PID/approve -H "Authorization: Bearer $SELLER"    # 403
curl -s -o /dev/null -w '%{http_code}\n' "$GW/api/products/review" -H "Authorization: Bearer $CUSTOMER"             # 403
curl -s -o /dev/null -w '%{http_code}\n' "$GW/api/audit/mine" -H "Authorization: Bearer $CUSTOMER"                  # 403
curl -fsS "$GW/api/products/review?status=Pending&pageSize=50" -H "Authorization: Bearer $ADMIN" \
  | jq '.items[] | select(.id=="'"$PID"'") | {name, sellerName, reviewStatus}'
```

**Expected**: the three refusals, and the product in the queue with its shop name.

---

## Scenario 4 - Approve once (US2 scenarios 1-2, FR-002, FR-008, FR-010)

```bash
curl -fsS -X POST $GW/api/products/$PID/approve -H "Authorization: Bearer $ADMIN" | jq .reviewStatus    # "Approved"
curl -s -X POST $GW/api/products/$PID/approve -H "Authorization: Bearer $ADMIN" | jq '{status, detail}' # 409
curl -s -o /dev/null -w '%{http_code}\n' $GW/api/products/$PID                                           # 200
curl -fsS "$GW/api/notifications?page=1&pageSize=5" -H "Authorization: Bearer $SELLER" | jq '.items[0].kind'  # "ProductApproved"
curl -fsS "$GW/api/audit/mine?pageSize=50" -H "Authorization: Bearer $ADMIN" \
  | jq '[.items[] | select(.subjectId=="'"$PID"'") | .action]'                                         # ["ProductApproved"]
```

**Expected**: on the shelf; the second approval is 409 (`This product is approved; nothing to do.` -
the detail is masked outside Development); one notice, one audit entry. In `ecommerce_activity_db`
(5440):

```sql
SELECT "Action", "ActorId" FROM audit_entries WHERE "SubjectId" = '<PID>' AND "Category" = 'Moderation';
SELECT "Kind", "Data" FROM notifications WHERE "Data"->>'product' = 'Review demo';
```

---

## Scenario 5 - An edit to the words sends it back; a price does not (US3, FR-012)

```bash
curl -fsS -X PUT $GW/api/products/$PID/variants/$PID/prices/VND -H "Authorization: Bearer $SELLER" \
  -H 'Content-Type: application/json' -d '{"amount":39000000}' >/dev/null
curl -fsS $GW/api/products/$PID -H "Authorization: Bearer $SELLER" | jq .reviewStatus             # "Approved"

curl -fsS -X PUT $GW/api/products/$PID/translations/vi -H "Authorization: Bearer $SELLER" \
  -H 'Content-Type: application/json' -d '{"name":"Review demo, renamed","description":"New words"}' | jq .reviewStatus   # "Pending"
curl -s -o /dev/null -w '%{http_code}\n' $GW/api/products/$PID                                   # 404
```

(The first variant reuses the product's id - specs/020 - hence `variants/$PID`. The body is
`VariantPriceRequest(decimal Amount)`.)

**Expected**: the price leaves it approved; the translation returns it to `Pending` and off the shelf.
An administrator making the same translation leaves an approved product approved (FR-004).

---

## Scenario 6 - Reject with a reason, resubmit (US2 scenarios 3-4, 7, FR-009, FR-011)

```bash
curl -s -X POST $GW/api/products/$PID/reject -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"reason":"   "}' | jq .status                        # 400
curl -fsS -X POST $GW/api/products/$PID/reject -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"reason":"Photograph the actual camera"}' | jq '{reviewStatus, reviewReason}'
curl -fsS -X POST $GW/api/products/$PID/resubmit -H "Authorization: Bearer $SELLER" | jq '{reviewStatus, reviewReason}'
# {"reviewStatus":"Pending","reviewReason":null}
curl -s -X POST $GW/api/products/$PID/resubmit -H "Authorization: Bearer $SELLER" | jq .status  # 409 - not rejected
```

---

## Scenario 7 - Take down (US2 scenario 5)

Approve it again, then:

```bash
curl -fsS -X POST $GW/api/products/$PID/take-down -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"reason":"Counterfeit"}' | jq '{reviewStatus, reviewReason}'
# {"reviewStatus":"Rejected","reviewReason":"Counterfeit"}
curl -s -o /dev/null -w '%{http_code}\n' $GW/api/products/$PID                                 # 404
```

---

## Scenario 8 - The automated checks

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~ProductReviewTests"
```

Seven tests, against the real PostgreSQL on 5433:

| Test | Proves |
| :-- | :-- |
| `A_sellers_new_product_is_hidden_and_unsellable_until_approved` | US1: not in search, lookup null, `PriceVariants` answers `Sellable = false`; seller and moderator see it |
| `The_shops_own_product_is_on_sale_as_listed` | FR-005 |
| `Approval_puts_it_on_the_shelf_tells_the_seller_and_happens_once` | FR-002, FR-010: second approval `ConflictException`; one notice with the link; one audit entry, actor the moderator |
| `A_rejection_says_why_and_the_seller_can_send_it_back` | FR-009, FR-011 |
| `Taking_down_an_approved_product_removes_it_from_the_shelf` | US2 scenario 5 |
| `Editing_the_name_sends_an_approved_product_back_and_a_price_does_not` | FR-012 |
| `Staff_see_the_queue_oldest_submission_first` | queue order |

Client:

```bash
cd client
npm test -- admin-products admin-moderation review-banner
```

Bruno, whole collection (the seller folder runs the product through every state):

```bash
cd bruno
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

The requests: `seller/the new product is not on the shelf yet` (404), `its seller sees it waiting`,
`a seller cannot approve their own product` (403), `staff see it in the review queue`,
`an administrator approves the product`, `staff see what they decided`,
`renaming it sends it back to review`, `the renamed product is off the shelf` (404),
`a moderator takes it down instead` (calls `/reject` - see below), `the seller sends it back`;
`security-checks/a customer cannot read the review queue` and `a customer has no decisions to read`
(403 each).

---

## Scenario 9 - The storefront

1. Sign in as a moderator and open `/admin`: it redirects to `/admin/moderation`, showing products and
   shops waiting (each a link) and your recent decisions.
2. `/admin/products`: tabs Pending, Rejected, Approved; Approve at a press; Reject and Take down open a
   dialog whose confirm stays disabled until a reason is typed.
3. As the seller, `/shop` shows a badge on a pending or rejected product; `/shop/products/:id` shows the
   banner - pending explains shoppers cannot see it; rejected shows the reason and **Send back for
   review**; approved warns that words and photographs are reviewed again.

---

## What was run at merge

From the pull request:

- Catalog tests 144/144, including the 7 `ProductReviewTests`; Identity 71/71; client 214/214, with
  lint, type-check and build clean.
- Bruno: 168/168 requests, 268 tests.
- `verify-saga.sh`: passes.
- Mutation checks, each red and then restored: dropping the listing filter fails the hidden-product
  test; letting `Sellable` ignore review fails the hidden and take-down tests; dropping the edit hook in
  the translation handler fails the rename test.
- Screenshots of the dashboard and the queue at 1360px, and the queue at 390px with no overflow.

Not run at merge, as far as the record says: the take-down endpoint through Bruno (the request titled
"a moderator takes it down instead" calls `/reject` on a pending product; a take-down request,
`a moderator takes the product down`, came with #169), and a concurrent double approval. The curl
scenarios above are written from the code; whether they were run by hand is not recorded.
