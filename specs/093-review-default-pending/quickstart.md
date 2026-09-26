# Quickstart: Validating the Pending default

**Feature**: [spec.md](spec.md) | **Data model**: [data-model.md](data-model.md)

## Prerequisites

```bash
cd server
docker compose up -d     # Catalog's PostgreSQL on 5433 and SeaweedFS on 8333
```

## Scenario 1 - The tests (US1, US2, SC-001, SC-002, SC-004)

```bash
cd server
dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~ProductReviewTests"
dotnet test tests/Ecommerce.Catalog.Tests
```

**Expected**: green, including `A_product_inserted_without_a_review_status_waits_for_review` and
`The_shops_own_product_is_on_sale_as_listed` (which reads the stored status). The fixture creates a new database and
runs every migration, this one included.

## Scenario 2 - The running database

```bash
cd server
dotnet ef database update --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/
```

```sql
-- psql -h localhost -p 5433 -U $DB_USER ecommerce_catalog_db
SELECT column_default FROM information_schema.columns WHERE table_name = 'products' AND column_name = 'ReviewStatus';
```

**Expected**: `'Pending'::character varying`. (The containers run the migration at startup.)

## Scenario 3 - Mutations (SC-003)

| Mutation | Expected red |
| :-- | :-- |
| The migration's `Up` sets `'Approved'` | `A_product_inserted_without_a_review_status_waits_for_review` |
| `.HasDefaultValue(ProductReviewStatus.Pending)` on the mapping | every test in the suite: EF refuses to migrate a model change no migration records; were a migration added too, `The_shops_own_product_is_on_sale_as_listed` would catch the stored `Pending` |
