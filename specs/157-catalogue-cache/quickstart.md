# Quickstart: The catalogue's public reads, served from memory

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests --filter "CatalogueCacheTests|CatalogueWritesTests"
./loadtest/run.sh browse          # several times on the stack before this change, several after (warm)
python loadtest/report.py         # docs/testing/load-test-results.md
```

By hand, against the stack:

```bash
curl -s -o /dev/null -w "%{time_total}\n" "http://localhost:5000/api/products?pageSize=12"   # twice: the second faster
# change a product's price as an administrator, then read again: the new price at once
```

Expected:
- the tests pass;
- the browse runs show a lower median and p95 for listing and search, with zero errors before and after;
- a write shows on the next anonymous read.
