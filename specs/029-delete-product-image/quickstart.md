# Quickstart: A deleted product takes its picture with it

> Completed on 2026-09-27, after the feature merged (#68), from the code at that merge, the pull request and docs/features/catalog.md.

## Prerequisites

The container overlay, so the image store is the `catalog_images` volume inside `ecommerce-catalog`:

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
export ADMIN_EMAIL=... ADMIN_PASSWORD=...          # on a line of its own
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json'   -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

The requests below written as `PUT /api/...` go through the gateway on `:5000` with
`-H "Authorization: Bearer $ADMIN"`; for example the upload is
`curl -X PUT http://localhost:5000/api/products/$ID/image -H "Authorization: Bearer $ADMIN" -F file=@camera.png`.

## Prove the defect first

Against a started stack, as an administrator:

```bash
# 1. give a product an image
PUT /api/products/{id}/image      # multipart, one part named "file"

# 2. look at the volume
docker exec ecommerce-catalog sh -c "ls /app/data/product-images"
#    -> one file named {productId-without-dashes}-{ticks}.png

# 3. delete the product
DELETE /api/products/{id}         # 204

# 4. look again
docker exec ecommerce-catalog sh -c "ls /app/data/product-images"
#    BEFORE this feature: the file is still there, and the product is 404
#    AFTER:               nothing
```

## The tests

```bash
cd server
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Catalog.Tests/ --filter ProductImageTests
```

Two new ones:

- `Deleting_a_product_deletes_its_image` - **must be seen failing before the fix.**
- `A_failing_store_does_not_stop_a_product_being_deleted` - `FailDeletes = true`, the product goes
  anyway.

And one that already exists and must keep passing: replacing an image leaves exactly one file.

## Clearing what is already orphaned

By hand, once, because two files is not a reason to build a sweeper (research D4):

```bash
docker exec ecommerce-catalog sh -c "ls /app/data/product-images"

# confirm each name's product really is gone - expect 404
curl -s -o /dev/null -w "%{http_code}\n" localhost:5000/api/products/<id-with-dashes>

docker exec ecommerce-catalog sh -c "rm /app/data/product-images/<file>"
```

⚠️ **Check the 404 for every file before removing it.** A key whose product still exists is a live
image, and deleting it leaves a row pointing at nothing - the exact failure the handler ordering is
designed to avoid.

## What was recorded at the merge (from the PR)

```
volume before          2 orphans (both products answer 404)
create product + image 200      -> 3 files
DELETE the product     204      -> 2 files   (its image is gone)
orphans removed by hand         -> 0 files
```

- `Deleting_a_product_deletes_its_image` was seen red before the fix:
  `Assert.Empty() Failure: Collection was not empty — ["01a0cc478733768b8e8d127e7ce6dc6e-…"]`.
- `A_failing_store_does_not_stop_a_product_being_deleted` passed before the fix for the wrong reason (the
  old code never called the store); removing the `try`/`catch` makes it fail with
  `IOException: Simulated storage failure`.
- 282 backend tests (Catalog 109 → 111); Bruno 91/91 requests, 142/142 tests.
