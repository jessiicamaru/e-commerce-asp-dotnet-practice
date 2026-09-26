# Quickstart: Validating one rule for "off the shelf"

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose up -d     # Catalog's PostgreSQL on 5433 and SeaweedFS on 8333
```

## Scenario 1 - The tests (US1, SC-001, SC-003)

```bash
cd server
dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~UnlistedProductReadsTests"
dotnet test tests/Ecommerce.Catalog.Tests
```

**Expected**: green, including `A_withdrawn_product_is_off_the_shelf_for_reads_as_it_is_for_writes`.

## Scenario 2 - By hand, through the gateway

```sql
-- psql -h localhost -p 5433 -U $DB_USER ecommerce_catalog_db
UPDATE products SET "IsActive" = false WHERE "Id" = '<an approved product>';
```

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/products/<id>            # 404
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/products/<id>/reviews    # 404
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/products/<id>/questions  # 404
```

Then restore it: `UPDATE products SET "IsActive" = true WHERE "Id" = '<id>';`

## Scenario 3 - One definition (US2, SC-004)

```bash
grep -rn "IsListed" server/src/Services/Catalog --include=*.cs | grep -v /obj/ | grep -v Migrations
```

**Expected**: only `Product.cs` (the definition and `OnShelf`) and `ProductConfiguration.cs` (`Ignore`).

## Scenario 4 - Mutations (SC-002)

| Mutation | Expected red |
| :-- | :-- |
| `OnShelf => IsListed` | `A_withdrawn_product_is_off_the_shelf_for_reads_as_it_is_for_writes` |
| The listing filter without `&& p.IsActive` | the same (its listing assertion) |
| `MaySee` on `product.IsListed` | the same (its lookup assertion) |
