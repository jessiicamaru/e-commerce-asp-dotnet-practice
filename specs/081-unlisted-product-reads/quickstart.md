# Quickstart: Validating what hangs on a product off the shelf

> Written on 2026-09-27, after the feature merged (#169), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

Each scenario names the success criterion it proves. The pull request records scenarios 1-4 as run against the
rebuilt stack (the issue's probe, Bruno, the browser flows); scenario 5 is the test suite.

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # or ./start-dev.sh
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

Pick a listed product and note its id as `P`. The seeded cameras have no photograph (`seed-catalogue.py` says
so), so give it one as an administrator, then read its image address:

```bash
curl -fsS -X PUT http://localhost:5000/api/products/$P/image -H "Authorization: Bearer $ADMIN" -F "file=@photo.png"
URL=$(curl -fsS http://localhost:5000/api/products/$P | jq -r .imageUrl)
echo "$URL"                      # /api/products/<P>/image?v=...&k=<32 hex>
```

---

## Scenario 1 - On sale, every address works (SC-003)

```bash
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/products/$P/image"        # 200
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000$URL"                            # 200
curl -sI "http://localhost:5000$URL" | grep -i cache-control      # public, max-age=31536000, immutable
```

## Scenario 2 - The issue's probe after a take-down (SC-001)

```bash
curl -fsS -X POST http://localhost:5000/api/products/$P/take-down -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"reason":"quickstart 081"}'
for path in "" /image /reviews /questions; do
  curl -s -o /dev/null -w "$path %{http_code}\n" "http://localhost:5000/api/products/$P$path"
done
```

**Expected**: all four `404`. Before #169 the last three were `200`. An id that never existed gives the same
`404` and the same `Product not found.` for reviews and questions.

## Scenario 3 - Only the image's own key opens it off the shelf (SC-001, SC-002)

```bash
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000$URL"                                  # 200
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/products/$P/image?k=$(uuidgen)"   # 404
curl -sI "http://localhost:5000$URL" | grep -i cache-control                                          # private, no-cache
```

Staff read it too: `curl -fsS http://localhost:5000/api/products/$P -H "Authorization: Bearer $ADMIN"` answers
`200` with the same `imageUrl`, and `GET /api/products/$P/reviews` with the admin token answers `200`.

## Scenario 4 - Bruno, the seller's side (SC-002)

```bash
cd bruno
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

The `seller/` folder runs, in order: `a moderator takes the product down` (seq 78), `its reviews are a 404 to a
stranger` (79), `its questions are a 404 to a stranger` (80), `its seller still reads its questions` (81). The pull
request records **267/267 requests, 433/433 tests**.

## Scenario 5 - The suite and its mutations (SC-004, SC-005)

```bash
cd server
DB_PASSWORD=<your password> SEAWEEDFS_ACCESS_KEY=... SEAWEEDFS_SECRET_KEY=... \
  dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~UnlistedProductReadsTests"
```

**Expected** at the merge: 7 tests green (the whole project 205/205), among them
`Replacing_the_photograph_retires_the_old_key` and `A_variants_own_photograph_follows_the_same_rule`. The pull
request records that each of five mutations turned them red: any key opening an unlisted image; reviews ignoring the
listing; questions ignoring the listing; a replacement keeping the old key; a variant image ignoring the key.

## Clean up

Put the product back on sale if it was a seeded one: the seller (or an administrator) sends it back
(`POST /api/products/$P/resubmit`) and staff approve it (`POST /api/products/$P/approve`).
