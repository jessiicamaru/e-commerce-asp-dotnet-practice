# Quickstart: Validating Product Deletion

> Written on 2026-09-27, after the feature merged (#61), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Contracts**: [http-api.md](./contracts/http-api.md), [messages.md](./contracts/messages.md)

Each scenario names what it proves. The results recorded at the merge come from the PR; anything not
recorded there is said to be so.

---

## Prerequisites

```bash
cd server
docker compose up -d        # Postgres for Catalog (5433) and Inventory (5437), RabbitMQ
./start-dev.sh              # Identity 5056, Catalog 5057, Inventory 5060, gateway 5000 at least
```

An administrator token, and a customer token for the negative case:

```bash
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

(Set `ADMIN_EMAIL` and `ADMIN_PASSWORD` on their own line first; see the Bruno note in CLAUDE.md about
prefix assignments.)

---

## Scenario 1 - The automated checks (FR-001 to FR-008)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~DeleteProductTests"
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Inventory.Tests --filter "FullyQualifiedName~ForgetProductTests"
```

**Expected**: 4 and 5 tests pass. They cover: the product, variants, options, prices and translations
gone; the event naming both variants; 404 for a product that is not there; the SKU reusable; a
forgotten variant answering 404 rather than zero; both variants forgotten; a second delivery forgetting
0; a bystander's count untouched; an empty list touching nothing.

**At the merge**: the PR reports 251 tests passing across the six test projects, 9 of them new.

---

## Scenario 2 - Delete through the gateway, and Inventory follows (User Stories 1 and 2, SC-001, SC-002)

1. Create a throwaway product as admin (`POST /api/products`) and note its `id`. It registers in
   Inventory at zero units through `ProductCreatedEvent`.
2. `curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/stock/$ID` → `200`.
3. Delete it:
   ```bash
   curl -s -o /dev/null -w '%{http_code}\n' -X DELETE http://localhost:5000/api/products/$ID -H "Authorization: Bearer $ADMIN"
   ```
   → `204`.
4. `GET /api/products/$ID` → `404`.
5. A few seconds later, `GET /api/stock/$ID` → `404`.

**At the merge**: the PR records a probe product answering `200` on `GET /api/stock/{id}`, being
deleted, and answering `404` four seconds later.

Confirm in Catalog's database that nothing is left:

```sql
-- psql -h localhost -p 5433 -U $DB_USER ecommerce_catalog_db
SELECT count(*) FROM product_variants WHERE "ProductId" = '<id>';   -- 0
SELECT count(*) FROM product_translations WHERE "ProductId" = '<id>';   -- 0
```

---

## Scenario 3 - A customer cannot delete (FR-002, SC-003)

Bruno, `security-checks/customer cannot delete a product (403)` - aimed at the collection's **real**
product on purpose: a 403 is the authorization check refusing before anything is read, and if it ever
returned 204 the rest of the run would collapse.

```bash
cd bruno
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

The `product/` folder's `create a product to delete` → `delete a product` (204) → `a deleted product
is gone` (404) run the happy path on a product made for the purpose.

**At the merge**: Bruno 85/85 requests, 126 tests.

---

## Scenario 4 - Clean a test-soiled catalogue (User Story 3, SC-004)

```bash
cd server
python seed/clean-test-debris.py          # prints "N camera(s) to keep, M product(s) to delete." and deletes nothing
python seed/clean-test-debris.py --yes    # deletes them; prints "<deleted> deleted; <left> product(s) left"
```

(`ADMIN_EMAIL` and `ADMIN_PASSWORD` in the environment; `GATEWAY_URL` defaults to
`http://localhost:5000`.)

**Expected**: only the SKUs in `seed/cameras.json` remain.

**At the merge**: 97 deleted, 14 left, and the shop opened with Canon EOS R50 at $799. ⚠️ Those 97
deletions ran before the Inventory consumer was registered and left their stock rows behind; they were
found by counting 125 stock rows against 23 variants and were not cleaned up.

---

## Scenario 5 - Orders are unaffected (FR-005)

Delete a product that has an order, then read the order (`GET /api/orders/{id}` as its customer).

**Expected**: the line still shows the frozen name, SKU, option summary and price. Whether this was
run by hand at the merge is not recorded; it follows from the frozen columns specs/009, 020, 021 and
022 added.
