# Quickstart: A deleted product takes its picture with it

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
