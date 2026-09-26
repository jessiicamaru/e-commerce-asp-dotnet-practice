# Quickstart: Validating the orphan report and reclaim

> Written on 2026-09-27, after the feature merged (#74), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/api.md](contracts/api.md)

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # or ./start-dev.sh
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
SELLER=...   # any seller's token (Bruno's seller folder makes one)
```

⚠️ Where the store is matters. At #74 it was the `catalog_images` volume, mounted at
`ProductImages:Root`; since specs/079 the containers use the SeaweedFS S3 bucket `product-images`
(`dotnet run` still defaults to the directory). Make the orphan in whichever store Catalog is reading.

---

## Scenario 1 — Nothing live is named (US1.2, SC-002)

```bash
curl -s http://localhost:5000/api/products/images/orphans -H "Authorization: Bearer $ADMIN" | jq
```

**Expected**: `200`. `liveKeys` is greater than zero whenever `scanned` is (zero live keys against a
full store would mean the catalogue read went wrong); no key in `orphans` belongs to a product that is
still shown with a picture; `note` names the one-instance assumption.

## Scenario 2 — An orphan is found (US1.1, SC-001)

Make an orphan the way the deliberate swallow leaves one: put a file with a valid key shape and no row
into the store, with a modification time older than the grace period.

```bash
# directory store (dotnet run, or the pre-079 volume)
printf 'orphan' > "$ROOT/00000000000000000000000000003039-638000000000000000.png"
touch -d '3 days ago' "$ROOT/00000000000000000000000000003039-638000000000000000.png"
curl -s http://localhost:5000/api/products/images/orphans -H "Authorization: Bearer $ADMIN" \
  | jq '.scanned, .liveKeys, .orphans'
```

**Expected**: the file is listed with its size and an `ageHours` of about 72 - what the pull request saw
(`scanned 17, liveKeys 16`, one 17-byte orphan 72 h old). A file touched a minute ago is **not** listed
(US1.3, SC-003).

## Scenario 3 — Asking changed nothing (US1.4)

Run scenario 2's `GET` twice. **Expected**: identical answers; the file is still in the store.

## Scenario 4 — Reclaim removes exactly that (US2)

```bash
curl -s -X DELETE http://localhost:5000/api/products/images/orphans -H "Authorization: Bearer $ADMIN" | jq
curl -s http://localhost:5000/api/products/images/orphans -H "Authorization: Bearer $ADMIN" | jq '.scanned, .liveKeys, .orphans'
```

**Expected**: the `DELETE` answers `200` with that one key in `orphans` and `failed: []`; afterwards
`scanned` equals `liveKeys` (16 and 16 on the pull request's run) and `orphans` is empty. Every product
picture in the storefront still loads.

## Scenario 5 — Only an administrator (US1.5, SC-005)

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/products/images/orphans -H "Authorization: Bearer $SELLER"
curl -s -o /dev/null -w '%{http_code}\n' -X DELETE http://localhost:5000/api/products/images/orphans -H "Authorization: Bearer $SELLER"
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/products/images/orphans
```

**Expected**: `403`, `403`, `401`. Bruno: `product/orphan images report` and
`security-checks/a seller cannot read the orphan report`. The collection deliberately never sends the
`DELETE`.

## Scenario 6 — The dangerous cases, automated (US3, SC-002 - SC-004)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~OrphanImageTests"
```

**Expected**: 8 pass - a live product's image and a live variant's image are never orphans; a file
written moments ago is never an orphan; **a catalogue that cannot be read reports nothing rather than
everything**; an orphan is found and can be reclaimed; a store refusing one key does not stop the rest;
the store's own bookkeeping is not waste; a row with no image contributes no key. At the merge Catalog
stood at 130 tests.

The failing-read case cannot be reproduced safely on a running stack; it is the test's job.
