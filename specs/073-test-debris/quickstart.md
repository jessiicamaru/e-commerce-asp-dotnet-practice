# Quickstart: Validating that test runs clean up

> Written on 2026-09-27, after the feature merged (#157), from the code at that merge, the pull request and
> docs/testing/testing-strategy.md.

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/README.md](contracts/README.md)

## Prerequisites

The full stack (the scripts need all six services; Bruno needs Mailpit too):

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

A count to compare against, from Catalog's database:

```bash
count() { docker exec <catalog-db-container> psql -U "$DB_USER" -d ecommerce_catalog_db -c \
  'SELECT (SELECT count(*) FROM products) AS products, (SELECT count(*) FROM categories) AS categories;'; }
count
```

---

## Scenario 1 - Two full Bruno runs (US1, SC-001)

```bash
cd bruno
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
for i in 1 2; do
  npx @usebruno/cli run --env local --env-var "baseUrl=http://localhost:8088" \
    --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
  count
done
```

**Expected** (the PR's run): 232/232 requests and 379/379 tests each time; the three `teardown` deletes 204; Catalog at
48 products and 19 categories before, between and after. Your counts are whatever they were before you started - the
point is that they do not move.

---

## Scenario 2 - The scripts (US1, SC-002)

```bash
cd server
count; ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh; echo "exit $?"; count
count; ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-auth.sh; echo "exit $?"; count
```

**Expected**: each passes (`approve=pass` for the saga), prints `cleanup: product … -> HTTP 204` and
`cleanup: category … -> HTTP 204`, exits 0, and the count is unchanged.

---

## Scenario 3 - A forced failure still cleans up and still fails (US2, SC-003)

With Payment approving (its default), force the other branch:

```bash
count; SAGA_E2E_SCENARIO=reject ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh; echo "exit $?"; count
```

**Expected** (the PR's run): exit code **1**; the cleanup lines still print 204 and 204; the count stays where it was
(48 in the PR).

---

## Scenario 4 - The folder order

```bash
grep -n "seq" bruno/seller/folder.yml bruno/teardown/folder.yml
```

**Expected**: `seller` has `info:` with `seq: 16`, `teardown` has `seq: 17`, so the CLI runs `teardown` last and after
`seller`, whose product it deletes.
