# Quickstart: Validating product images in object storage

> Written on 2026-09-27, after the feature merged (#163), from the code at that merge, the pull request and docs/features/catalog.md (with docs/infrastructure/running-in-containers.md).

**Feature**: [spec.md](./spec.md) | **Contracts**: [http-api.md](./contracts/http-api.md), [README.md](./contracts/README.md)

How to show the feature works. Each scenario names the story or success criterion it proves. Where the PR recorded
a result, it is quoted; where it did not record how a step was done, this file says so.

---

## Prerequisites

`server/.env` needs the two SeaweedFS keys (any values; they only have to agree):

```bash
SEAWEEDFS_ACCESS_KEY=your_seaweedfs_access_key
SEAWEEDFS_SECRET_KEY=your_seaweedfs_secret_key
```

Compose refuses to start without them, as it does for every other secret:

```bash
cd server
docker compose up -d          # infrastructure, now including seaweedfs on :8333
docker ps --filter name=e-commerce-seaweedfs --format '{{.Names}} {{.Status}}'   # expect "(healthy)"
curl -fsS -o /dev/null -w '%{http_code}\n' http://localhost:8333/healthz          # expect 200
```

An administrator token, through the gateway:

```bash
TOKEN=$(curl -fsS -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

Before trusting any container result, no stray host process may be answering on Catalog's port
(`Get-Process | Where-Object { $_.ProcessName -like 'Ecommerce.*' }` must be empty; CLAUDE.md, "Running in
containers").

---

## Scenario 1 - The store's guarantees against a real S3 server (FR-001, FR-002, SC-005)

```bash
cd server
DB_PASSWORD=<your password> SEAWEEDFS_ACCESS_KEY=... SEAWEEDFS_SECRET_KEY=... \
  dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~S3ProductImageStoreTests"
```

**Expected**: 12 passed - the round trip and missing key, a key stored once, four unsafe keys refused before any
request, the listing paged at 2 without the probe, two instances seeing each other's writes and deletes, the orphan
scan identical from either instance, the import copying what is missing then nothing, four concurrent imports copying
each image once with no failure, and a wrong secret or missing settings refusing to start. Each run makes and removes
its own `catalog-tests-<guid>` bucket.

The whole project, as the PR ran it: `DB_PASSWORD=... SEAWEEDFS_ACCESS_KEY=... SEAWEEDFS_SECRET_KEY=... dotnet test
tests/Ecommerce.Catalog.Tests` - **198/198** at the merge.

Without the two keys set, the S3 tests fail with `SEAWEEDFS_ACCESS_KEY is not set: the S3 store's tests sign their
requests with it (see server/.env).` rather than being skipped.

---

## Scenario 2 - The containers come up on the bucket and import the old volume (US3, FR-005, SC-004)

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
docker logs ecommerce-catalog 2>&1 | grep "Product images imported"
```

(`ecommerce-catalog` is the `container_name` in `docker-compose.app.yml`; SeaweedFS's is `e-commerce-seaweedfs`, with a
hyphen, because it lives in the infrastructure file.)

**Expected**: one line of the form

```text
Product images imported from /app/legacy/product-images into bucket product-images: N copied, M already there, 0 failed.
```

On the first start after the move, `N` is the number of images the `catalog_images` volume holds. The PR measured
**14 copied**. After a restart, `0 copied, 14 already there` - the import is idempotent and runs at every start while
the volume is mounted.

Confirm the volume is untouched and read-only:

```bash
docker compose -f docker-compose.yml -f docker-compose.app.yml exec catalog sh -c 'ls /app/legacy/product-images | wc -l; touch /app/legacy/x'
```

**Expected**: the same count as before the start, and `touch` fails with a read-only file system.

---

## Scenario 3 - Two instances serve every image (US1, SC-001, SC-003)

The PR ran two Catalog containers over the one bucket; **how the second was started is not recorded**. One way,
publishing the second on an unused host port:

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml run -d --name catalog-2 -p 5957:8080 catalog
```

Upload through the gateway (instance 1), read through instance 2:

```bash
PRODUCT=<a product id>
curl -fsS -X PUT "http://localhost:5000/api/products/$PRODUCT/image" \
  -H "Authorization: Bearer $TOKEN" -F "file=@photo.png;type=image/png" | jq .imageUrl
curl -fsS "http://localhost:5000/api/products/$PRODUCT/image" -o via-1.png
curl -fsS "http://localhost:5957/api/products/$PRODUCT/image" -o via-2.png
cmp via-1.png via-2.png && echo identical
```

**Expected**: `identical`. The PR recorded the image served **byte-for-byte** by instance 2, and instance 2 serving
**15/15** product images.

Delete through instance 2, then read through instance 1:

```bash
curl -fsS -X DELETE "http://localhost:5957/api/products/$PRODUCT" -H "Authorization: Bearer $TOKEN" -o /dev/null -w '%{http_code}\n'   # 204
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/products/$PRODUCT/image"                                        # 404
```

**Expected**: 204, then 404 - as the PR recorded. Use a throwaway product: this deletes it.

Remove the second instance afterwards: `docker rm -f catalog-2`.

---

## Scenario 4 - The orphan report is the same from either instance (US2, FR-007, SC-002)

```bash
curl -fsS http://localhost:5000/api/products/images/orphans -H "Authorization: Bearer $TOKEN" | jq '{scanned, liveKeys, orphans: (.orphans|length), note}'
curl -fsS http://localhost:5957/api/products/images/orphans -H "Authorization: Bearer $TOKEN" | jq '{scanned, liveKeys, orphans: (.orphans|length), note}'
```

**Expected**: the same numbers from both, 0 orphans, and `note` beginning `The store is shared object storage
(specs/079)`. The PR recorded **15 scanned, 15 live, 0 orphans** from both.

Against `dotnet run` (the directory store) the `note` still begins `One Catalog instance is assumed (specs/019)`.

---

## Scenario 5 - A store that cannot be used stops Catalog (US4, FR-002, FR-003)

Run Catalog on the host with the S3 store and a wrong secret:

```bash
cd server
ProductImages__Store=S3 ProductImages__S3__ServiceUrl=http://localhost:8333 ProductImages__S3__Bucket=product-images \
ProductImages__S3__AccessKey=$SEAWEEDFS_ACCESS_KEY ProductImages__S3__SecretKey=wrong \
  dotnet run --project src/Services/Catalog/Ecommerce.Catalog.WebApi/
```

**Expected**: after about 30 seconds, the process stops with `Product images cannot be stored: bucket
'product-images' at http://localhost:8333 could not be written (...)`. Leaving out `ProductImages__S3__Bucket`
instead stops it at once with `ProductImages:S3:Bucket is not set.`; `ProductImages__Store=Blob` stops it with
`ProductImages:Store must be FileSystem or S3, not 'Blob'.`

This scenario is covered by `A_store_that_cannot_write_refuses_to_start_and_says_why` in Scenario 1; whether it was
also run by hand is not recorded.

---

## Scenario 6 - The API collection through the rebuilt containers (FR-012, SC-007)

```bash
cd bruno
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: every request green. The PR recorded **263/263 requests, 428/428 tests**; the counts have grown since.
`product/orphan images report` passes with either store's note.

---

## Mutation checks, as the PR recorded them

Each of these, applied by hand, turned `S3ProductImageStoreTests` red:

| Mutation | Test that failed |
| :-- | :-- |
| No `If-None-Match` | The stored-once and concurrent-import tests |
| The probe not skipped | The listing test |
| Only the first page listed | The listing test |
| A missing key throwing | 4 tests |
| `SharedAcrossInstances` false | The orphan-report and two-instances tests |
| A concurrent import counting a 412 as a failure | The concurrent-import test |
| Any key accepted | The unsafe-key test |
