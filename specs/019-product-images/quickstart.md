# Quickstart: Product Images

## Tests

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests
```

`ProductImageTests` runs against real PostgreSQL, with the real file store in a temporary directory.

## Against the stack

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build catalog
cd ../bruno
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

Expected: `product/upload image` 200; `product/get image` 200 `image/png` with the immutable cache
header; `security-checks/` wrong type 400, too big 400, customer upload 403.

## By hand

```bash
curl -X PUT -H "Authorization: Bearer $ADMIN" -F "file=@bruno/fixtures/pixel.png" \
     http://localhost:5000/api/products/$ID/image          # -> imageUrl
curl -I "http://localhost:5000$IMAGE_URL"                  # -> image/png, immutable
docker compose -f docker-compose.yml -f docker-compose.app.yml restart catalog
curl -I "http://localhost:5000$IMAGE_URL"                  # -> still 200: the volume kept it
```

Then open the storefront: the product shows the picture, and the others still show the letter tile.
