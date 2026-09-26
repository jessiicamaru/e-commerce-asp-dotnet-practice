# Quickstart: A Catalogue of Real Cameras

> Written on 2026-09-27, after the feature merged (#60), from the code at that merge, the pull request
> and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # every service and RabbitMQ
```

Python 3 (standard library only). `ADMIN_EMAIL` / `ADMIN_PASSWORD` are the seeded administrator's (they
are in `server/.env`). Set them on their own line - `ADMIN_EMAIL=... cmd "$ADMIN_EMAIL"` on one line
expands to an empty string (CLAUDE.md).

## Scenario 1 - Seed (US1, US2, SC-001)

```bash
cd server
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
python seed/seed-catalogue.py
```

**Expected**: the approximate-price warning, `administrator signed in`, one `ok` per category, product
and extra variant, then `14 product(s) added, 0 already there` on an empty catalogue, and the no-images
note. If a stock write finds Inventory has not registered a variant yet, the run waits rather than
failing.

## Scenario 2 - Run it again (US2, SC-002)

```bash
python seed/seed-catalogue.py
```

**Expected**: `0 product(s) added, 14 already there` - as the pull request recorded. Translations,
dollar prices and stock are rewritten; nothing is added or deleted.

## Scenario 3 - Read it back in both languages and currencies (US1, US3)

```bash
ID=$(curl -s 'http://localhost:5000/api/products?searchTerm=FUJI-XT5&pageSize=1' | jq -r '.items[0].id')
curl -s "http://localhost:5000/api/products/$ID?lang=vi&currency=VND" | jq '.variants[] | {sku, optionSummary, price}'
curl -s "http://localhost:5000/api/products/$ID?lang=en&currency=USD" | jq '.variants[] | {sku, optionSummary, price}'
```

**Expected**: for the body-only black X-T5, as the pull request recorded:

```text
vi/VND   Bộ: Chỉ thân máy · Màu: Đen          42.000.000 ₫
en/USD   Kit: Body only · Colour: Black       $1,699.00
```

The options are in the same order in both - `Bộ`/`Kit` first, because the order is by the stored
(Vietnamese) name - and every variant's options carry an `id`.

## Scenario 4 - The stored summaries are in order (FR-008)

```bash
docker exec -i ecommerce-catalog-db psql -U "$DB_USER" -d ecommerce_catalog_db -c '
SELECT count(*) AS out_of_order
FROM product_variants v
WHERE v."OptionSummary" <> COALESCE((
  SELECT string_agg(o."Name" || '"'"': '"'"' || o."Value", '"'"' · '"'"' ORDER BY lower(o."Name"))
  FROM variant_options o WHERE o."VariantId" = v."Id"), '"'"''"'"');'
```

**Expected**: `0`. Not recorded as run in this form; the pull request's evidence is the read-back in
scenario 3 and the test in scenario 5.

## Scenario 5 - The tests (US3, US4, SC-003)

```bash
cd server
SEAWEEDFS_ACCESS_KEY=... SEAWEEDFS_SECRET_KEY=... DB_PASSWORD=... \
  dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~VariantTests"
```

(The S3 keys are needed by today's Catalog tests; at the merge only `DB_PASSWORD` was.)

**Expected**: `Two_shapes_of_one_product_list_their_options_in_the_same_order` and
`An_option_carries_its_id_so_it_can_be_addressed` pass.

**Negative control**: the pull request records that the ordering test was "verified to go red when the
fix is removed". Which lines were removed is not recorded; removing the `OrderBy` from
`ProductVariant.Summarise` is the direct way to repeat it. Restore it afterwards.

## Scenario 6 - Nothing else broke (SC-004)

```bash
DB_PASSWORD=... dotnet test
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-auth.sh
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
cd ../bruno && npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected** at the merge: 245 tests (Catalog 78, Order 61, Identity 50, Inventory 26, Cart 14, Payment
13); Bruno 81/81 requests, 122 tests; both scripts pass.
