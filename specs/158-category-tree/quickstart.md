# Quickstart: Categories in a tree - departments and their categories

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests --filter CategoryTreeTests
python seed/catalogue.py
ADMIN_EMAIL=... ADMIN_PASSWORD=... ADMIN_TOTP_SECRET=... python seed/seed-catalogue.py      # twice
cd ../client && npm test
```

Expected:
- the tests pass; the checker lists 6 departments;
- after seeding, `GET /api/categories` has 19 rows: 6 with no parent, 13 under them; the second run changes nothing;
- `GET /api/products?categoryId=<Điện tử>` lists 7 products (phones, laptops, headphones);
- the storefront's filter shows each department with its categories indented, the hero's chips are the 6 departments,
  a phone's page shows "Điện tử › Điện thoại";
- the back office's categories page shows the tree; moving "Laptop" to the top level shows at once in the storefront.
