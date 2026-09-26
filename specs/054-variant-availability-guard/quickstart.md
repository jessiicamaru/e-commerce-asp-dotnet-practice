# Quickstart: Validating the variant availability guard

> Written on 2026-09-27, after the feature merged (#137), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/messages.md](contracts/messages.md)

## Prerequisites

```bash
cd server
docker compose up -d        # Catalog's database on 5433, SeaweedFS on 8333 for the Catalog test fixture
```

`SEAWEEDFS_ACCESS_KEY` / `SEAWEEDFS_SECRET_KEY` set, as CLAUDE.md says for `Ecommerce.Catalog.Tests` today (at
this feature's merge the fixture did not yet need S3; it arrived with specs/079).

## Scenario 1 - The reordering (US1 scenario 1, SC-001)

```bash
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests \
  --filter "FullyQualifiedName~AvailabilityTests.A_repeated_value_still_moves_the_clock_so_an_older_contrary_one_loses"
```

**Expected**: green. It records in stock at t1, in stock at t3, then out of stock at t2 (which returns false),
and reads the product back in stock, observed at t3. It failed before the fix.

## Scenario 2 - The controls (US1 scenarios 2 and 3, SC-002)

```bash
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~AvailabilityTests"
```

**Expected**: every test green, including the existing duplicate, overtaken and `A_newer_observation_does_win`
tests. The whole project passed 153/153 at the merge.

## Scenario 3 - The mutation

Put back `&& (v.Availability != isAvailable || v.AvailabilityObservedAt == null)` in
`ProductRepository.TryRecordVariantAvailabilityAsync` and rerun scenario 1: it goes red (this is the pre-fix run
the pull request reports). Restore.

## Scenario 4 - In the database (by hand)

```sql
-- ecommerce_catalog_db on 5433
SELECT "Id", "Availability", "AvailabilityObservedAt" FROM product_variants WHERE "ProductId" = '<product id>';
```

Whenever Inventory announces the variant again with the same value (an announcement follows a stock movement
such as `PUT /api/stock/{variantId}` through :5000), `AvailabilityObservedAt` advances although `Availability` does
not change. Not recorded as run for this feature.
