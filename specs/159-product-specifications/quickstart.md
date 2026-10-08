# Quickstart: Product specifications per category

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests --filter "ProductSpecificationTests|MyDataTests|CatalogueWritesTests"
python seed/catalogue.py
ADMIN_EMAIL=... ADMIN_PASSWORD=... ADMIN_TOTP_SECRET=... python seed/seed-catalogue.py      # twice
cd ../client && npm test
```

Expected:
- the tests pass; the checker validates every product's specifications;
- after seeding, `GET /api/products/{id}?lang=en` of the Sony A7 IV carries Brand: Sony, Sensor: Full-frame,
  Resolution: 33 MP; the second run changes nothing;
- `GET /api/categories/{Electronics}/specifications` lists Brand with its options; filtering Electronics by Apple lists
  the 3 Apple products;
- the storefront's product page shows the table; choosing Electronics offers Brand; the seller's product page fills
  specifications in; the back office lists them per category.
