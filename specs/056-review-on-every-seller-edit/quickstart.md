# Quickstart: Validating review on every seller edit

> Written on 2026-09-27, after the feature merged (#139), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose up -d        # Catalog's database on 5433
```

For scenario 3: the stack running, a seller (`SELLER` token) with an approved product `PID`, an administrator
(`ADMIN` token).

## Scenario 1 - The tests (US1, US2, SC-001 to SC-003)

```bash
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~ProductReviewTests"
```

**Expected**: green, including `A_seller_adding_a_variant_sends_an_approved_product_back_and_staff_do_not` and
`A_seller_translating_an_option_sends_an_approved_product_back_and_it_is_on_the_record`. Both failed before the fix
with status `Approved` where `Pending` was expected. The whole project: 157 tests at the merge.

## Scenario 2 - Mutation checks

Remove the `AfterSellerEditAsync` call from `AddProductVariantCommandHandler`, rerun: red. Restore; remove it from
`SetOptionTranslationCommandHandler`, rerun: red. Restore. (The pull request's two mutations.)

## Scenario 3 - Through the gateway (by hand)

```bash
# the seller adds a variant to their approved product
curl -s -X POST "http://localhost:5000/api/products/$PID/variants" -H "Authorization: Bearer $SELLER" \
  -H 'Content-Type: application/json' -d '{"sku":"QS-056-1","price":42000000,"options":[{"name":"Kit","value":"With lens"}]}' \
  -o /dev/null -w '%{http_code}\n'
curl -s "http://localhost:5000/api/products/$PID" -H "Authorization: Bearer $SELLER" | jq .reviewStatus
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/products/$PID"      # anonymous
```

**Expected**: `201`; `"Pending"`; `404`. With `$ADMIN` instead of `$SELLER` on an approved product the status stays
`"Approved"`. The audit log (`GET /api/audit`, Admin) holds `VariantAdded` and `ProductSentForReview` for the
product. Not recorded as run for this feature - the pull request's evidence is the test suite.
