# Quickstart: A shop for anything, not only cameras

```bash
cd server
python seed/catalogue.py                                     # every vertical valid
ADMIN_EMAIL=... ADMIN_PASSWORD=... ADMIN_TOTP_SECRET=... python seed/seed-catalogue.py   # twice
ADMIN_EMAIL=... ADMIN_PASSWORD=... ADMIN_TOTP_SECRET=... python seed/clean-test-debris.py
cd ../client && npm test && npm run lint && npm run build
```

Expected:
- the checker prints each vertical with its counts and exits 0;
- the first seeding run adds the new products (cameras already there are skipped); the second adds nothing;
- the cleaner keeps every seeded SKU and has nothing seeded to delete;
- the storefront (`http://localhost:8088`) lists every new category in its filter, and a product without a photograph
  shows the parcel tile; the search box reads "Search products…";
- client tests pass; with "Search cameras…" put back, the wording test fails.
