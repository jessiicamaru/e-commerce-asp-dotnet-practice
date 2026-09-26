# Quickstart: The Shop Is a Marketplace

> Written on 2026-09-27, after the feature merged (#64), from the code at that merge, the pull request and docs/features/marketplace.md.

**Feature**: [spec.md](./spec.md) | **Contracts**: [api.md](./contracts/api.md), [messages.md](./contracts/messages.md)

---

## Prerequisites

```bash
cd server
docker compose up -d                              # Postgres, RabbitMQ
dotnet ef database update --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ \
                          --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/    # AddSellerProfilesAndOutbox
dotnet ef database update --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ \
                          --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/      # AddSellers
./start-dev.sh
export ADMIN_EMAIL=... ADMIN_PASSWORD=...         # on a line of its own
```

A helper for the requests below:

```bash
register() {   # email, shop name -> access token
  curl -s -X POST http://localhost:5000/api/auth/register-seller -H 'Content-Type: application/json' \
    -d '{"email":"'"$1"'","password":"Str0ng!Pass","firstName":"T","lastName":"T","shopName":"'"$2"'"}' | jq -r .token
}
ALICE=$(register alice-$RANDOM@example.test "Alice Cameras")
BOB=$(register bob-$RANDOM@example.test "Bob Lenses")
```

---

## Scenario 1 - The automated checks (SC-001, SC-004)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~SellerOwnershipTests"
```

**Expected**: 16 pass - a seller's product records them and reads back with their shop name; an
administrator's belongs to the shop; eight cross-seller writes are each refused (delete, add variant,
reprice, translate, remove translation, translate an option, price in another currency, remove a price);
the shop's own product cannot be adopted; a seller may do all of it to their own; an administrator may touch
anybody's; "my listings" holds mine only; a rename changes products without writing one; an overtaken rename
does not win.

**At the merge**: 278 tests across the six projects, 16 new.

---

## Scenario 2 - A seller lists a product and the shop name reaches it (User Story 1)

```bash
CAT=$(curl -s http://localhost:5000/api/categories | jq -r '.[0].id')
P=$(curl -s -X POST http://localhost:5000/api/products -H "Authorization: Bearer $ALICE" -H 'Content-Type: application/json' \
  -d '{"name":"Alice camera","description":null,"price":1000000,"sku":"ALICE-'$RANDOM'","categoryId":"'$CAT'"}' | jq -r .id)
sleep 3                                            # the registration event reaching Catalog
curl -s http://localhost:5000/api/products/$P | jq '{sellerId, sellerName}'   # "Alice Cameras"
curl -s http://localhost:5000/api/products/mine -H "Authorization: Bearer $ALICE" | jq .totalCount   # 1
curl -s http://localhost:5000/api/products/mine -H "Authorization: Bearer $BOB"   | jq .totalCount   # 0
```

A plain customer posting the same body gets `403`.

---

## Scenario 3 - Somebody else's listing is a 404 (User Story 2, SC-001)

```bash
V=$(curl -s http://localhost:5000/api/products/$P | jq -r '.variants[0].id')
curl -s -o /dev/null -w '%{http_code}\n' -X PUT http://localhost:5000/api/products/$P/variants/$V/prices/USD \
  -H "Authorization: Bearer $BOB" -H 'Content-Type: application/json' -d '{"amount":10}'        # 404
curl -s -o /dev/null -w '%{http_code}\n' -X DELETE http://localhost:5000/api/products/$P -H "Authorization: Bearer $BOB"   # 404
```

**Expected**: `404`, never `403`. **At the merge**: on the containerised stack Alice got 200/201 on her own and
Bob got 404 on all four of Alice's he tried. Bruno's `seller/a seller cannot touch the shops product` asserts
the **exact** status, which is what told the real 404 from the earlier, wrong-reason 403.

---

## Scenario 4 - Renaming writes no product (FR-008, SC-004)

```bash
psql -h localhost -p 5433 -U $DB_USER ecommerce_catalog_db -c "SELECT \"UpdatedAt\" FROM products WHERE \"Id\" = '$P'"
curl -s -X PUT http://localhost:5000/api/sellers/me/shop-name -H "Authorization: Bearer $ALICE" \
  -H 'Content-Type: application/json' -d '{"shopName":"Alice Optics"}'
sleep 3
curl -s http://localhost:5000/api/products/$P | jq .sellerName                                  # "Alice Optics"
psql -h localhost -p 5433 -U $DB_USER ecommerce_catalog_db -c "SELECT \"UpdatedAt\" FROM products WHERE \"Id\" = '$P'"   # unchanged
```

⚠️ **Let a new product settle first.** The PR's first rename check reported "product row: WRITTEN": the
stock-availability announcement for a product created seconds earlier lands a few seconds later and writes
`UpdatedAt`. Re-measured after it settled, the row was untouched.

---

## Scenario 5 - Identity without a broker (research D6)

```bash
docker stop e-commerce-rabbitmq
CAROL=$(register carol-$RANDOM@example.test "Carol Film")                 # still 200, a real token
psql -h localhost -p 5435 -U $DB_USER ecommerce_identity_db -c 'SELECT count(*) FROM "OutboxMessage"'   # >= 1
docker start e-commerce-rabbitmq                                          # the outbox drains to 0; Catalog learns "Carol Film"
```

**At the merge**: exactly this was observed. CI's `auth-smoke` job runs Identity with no broker and keeps
passing.

---

## Scenario 6 - Existing products still sell (User Story 4, SC-003)

`../.github/scripts/verify-saga.sh` and `verify-auth.sh` (both passed at the merge); a seeded camera reads
`sellerId: null` and the storefront says "Sold by The shop". Bruno at the merge: 91/91 requests, 138 tests.
